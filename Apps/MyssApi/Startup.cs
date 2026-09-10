namespace Myss.Api
{
    using System;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Configuration;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Data;
    using Myss.Api.Providers;
    using Myss.Api.Services;

    /// <summary>
    /// Configures the application during startup.
    /// </summary>
    public class Startup
    {
        private readonly StartupConfiguration startupConfig;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="env">The injected Environment provider.</param>
        /// <param name="configuration">The injected configuration provider.</param>
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            this.startupConfig = new StartupConfiguration(configuration, env);
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The injected services provider.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            this.startupConfig.ConfigureForwardHeaders(services);
            this.startupConfig.ConfigureHttpServices(services);
            this.startupConfig.ConfigureSwaggerServices(services);
            this.startupConfig.ConfigureTracing(services);

            // Authentication is the Option 1 / Option 2 swap point; the policies and the
            // typed caller below are permanent and unaffected by that choice.
            this.startupConfig.ConfigureAuthentication(services);
            services.AddMyssAuthorization();

            // Effective roles are computed in one place (RoleCalculator, ADR-0007): the
            // shared standard realm cannot know MySS account state, so a citizen IDP grants
            // CLIENT here rather than via per-user CSS role assignment. Runs after every
            // scheme, mock auth included (a pass-through for persona principals).
            services.AddSingleton<IClaimsTransformation, RoleCalculationClaimsTransformation>();
            services.AddTransient<ICurrentUserAccessor, CurrentUserAccessor>();

            // Configure the demo services
            services.AddTransient<IDemoService, DemoService>();
            services.AddSingleton<IDemoProvider, DemoProvider>();

            // Configure the forms module (POC: spec proxy + versioned submissions).
            // Protected behind authentication (see FormsController [Authorize]).
            services.AddDbContext<FormsDbContext>(options =>
                options.UseNpgsql(this.startupConfig.Configuration.GetConnectionString("FormsDb")));
            services.AddHttpClient<IFormSpecProvider, StrapiFormSpecProvider>();
            services.AddHttpClient<IPdfProvider, CdogsPdfProvider>();
            services.AddSingleton<ITemplateProvider, EmbeddedTemplateProvider>();
            services.AddScoped<IFormsService, FormsService>();

            // Eligibility Estimator (Option B): the browser computes the estimate;
            // MyssApi serves the Form.io spec and the rate table anonymously (see
            // EligibilityEstimatorController). The rate provider reads Strapi and
            // falls back to the compiled MYSS-25 table; the result is cached so the
            // public endpoint does not hit Strapi on every request.
            services.AddMemoryCache();
            services.AddHttpClient<IEligibilityRateProvider, StrapiEligibilityRateProvider>(
                client => client.Timeout = TimeSpan.FromSeconds(5));

            // Configure the attachments module: validate -> quarantined row ->
            // ClamAV scan -> object store -> release. Protected behind
            // authentication (see AttachmentsController [Authorize]).
            IConfiguration configuration = this.startupConfig.Configuration;
            services.Configure<AttachmentsConfig>(configuration.GetSection("Attachments"));
            services.Configure<ClamAvConfig>(configuration.GetSection("ClamAv"));
            services.Configure<ObjectStorageConfig>(configuration.GetSection("ObjectStorage"));
            services.AddDbContext<AttachmentsDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("AttachmentsDb")
                        ?? configuration.GetConnectionString("FormsDb"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "attachments")));
            services.AddSingleton<IVirusScanProvider, ClamAvScanProvider>();

            // No storage config, no startup — same fail-closed idea as the
            // mock-auth gate. Silently accepting files we can't store would be
            // worse than crashing.
            ObjectStorageConfig objectStorage = new();
            configuration.GetSection("ObjectStorage").Bind(objectStorage);
            if (!objectStorage.IsConfigured)
            {
                throw new InvalidOperationException(
                    "ObjectStorage is not configured (ServiceUrl, Bucket, AccessKey and SecretKey are all required). "
                    + "Locally: `docker compose up -d minio minio-init` and run with the Development settings. "
                    + "Deployed: set Myss_ObjectStorage__ServiceUrl/Bucket/AccessKey/SecretKey from the secret.");
            }

            services.AddSingleton<IFileStorageProvider, S3FileStorageProvider>();
            services.AddScoped<IAttachmentsService, AttachmentsService>();

            // Bus pass hand-off to ICM. MyssApi never calls Siebel: everything
            // ICM-bound goes to the IcmApi middleware over REST, which owns the
            // ICM credentials and the Siebel translation (handbook Part 4.2 /
            // 8.1). This side owns the resilience envelope on the named client.
            services.Configure<IcmApiConfig>(configuration.GetSection("IcmApi"));
            IcmApiConfig icmApi = new();
            configuration.GetSection("IcmApi").Bind(icmApi);
            if (!icmApi.IsConfigured)
            {
                // Same fail-closed idea as ObjectStorage above: only the
                // non-secret base URL is required to boot; the service
                // credentials are checked at the first call.
                throw new InvalidOperationException(
                    "IcmApi is not configured (BaseUrl is required). "
                    + "Locally: appsettings.Development.json points at a local middleware. "
                    + "Deployed: set Myss_IcmApi__BaseUrl to the in-cluster icm-api Service.");
            }

            services.TryAddSingleton(TimeProvider.System);

            // One correlation id per request, stamped on the dispatch log and
            // forwarded to the middleware as X-Request-ID (handbook Part 4.12).
            // Singleton over IHttpContextAccessor, which is async-local, so the
            // client factory's handler chain sees the current request too.
            services.AddHttpContextAccessor();
            services.TryAddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
            services.AddTransient<CorrelationIdForwardingHandler>();
            services.AddHttpClient(IcmApiBusPassSubmissionProvider.HttpClientName, client =>
                {
                    // A trailing slash so the relative route appends instead
                    // of replacing the last path segment.
                    client.BaseAddress = new Uri(icmApi.BaseUrl!.AbsoluteUri.TrimEnd('/') + "/");

                    // The resilience handler owns the attempt and total
                    // timeouts; the client's own must sit above them or it
                    // fires first and hides the real cause.
                    client.Timeout = TimeSpan.FromSeconds(icmApi.TimeoutSeconds * (IcmApiResilience.MaxRetryAttempts + 1))
                        + IcmApiResilience.TotalTimeoutMargin
                        + TimeSpan.FromSeconds(5);
                })
                .AddHttpMessageHandler<CorrelationIdForwardingHandler>()
                .AddStandardResilienceHandler(options => IcmApiResilience.Configure(options, icmApi));
            services.AddSingleton<IBusPassSubmissionProvider, IcmApiBusPassSubmissionProvider>();
            services.AddScoped<IBusPassSubmissionService, BusPassSubmissionService>();

            // CORS services are required by the inline UseCors policy in
            // StartupConfiguration.UseHttp, which is driven by the AllowOrigins config.
            services.AddCors();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        public void Configure(IApplicationBuilder app)
        {
            this.startupConfig.UseForwardHeaders(app);
            this.startupConfig.UseHttp(app);
            this.startupConfig.UseResponseCaching(app);
            this.startupConfig.UseEnrichTracing(app);
            this.startupConfig.UseRest(app);
            this.startupConfig.UseSwagger(app);
        }
    }
}
