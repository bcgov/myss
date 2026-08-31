namespace Myss.Api
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
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
