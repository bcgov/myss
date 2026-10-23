namespace Myss.AdminApi
{
    using System;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Intake;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Services;
    using Myss.Data;
    using Myss.Data.Intake;
    using Myss.Data.Platform;
    using Myss.Common.Auth;
    using Myss.Common.Content;
    using Myss.Common.Hosting;

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
            // typed caller below are unaffected by that choice. Effective roles are
            // computed by the same RoleCalculator as MyssApi's (MyssCommon, ADR-0007), so
            // WorkerWithIdir means the same here: in particular a citizen-IDP token
            // never keeps WORKER or ADMIN. Runs after every scheme, mock auth included.
            this.startupConfig.ConfigureAuthentication(services, mockDefaultPersona: "admin");
            services.AddMyssAuthorization();
            services.AddSingleton<IClaimsTransformation, RoleCalculationClaimsTransformation>();
            services.AddTransient<ICurrentUserAccessor, CurrentUserAccessor>();

            // The admin form editor (MYSS-209). The only Strapi token this API holds is
            // the write-scoped Strapi:AdminApiToken; the citizen API keeps the read-only
            // one. Brokered server-side; the token never reaches the browser. The
            // shared readers (MyssCommon) read with it too: it also grants find and
            // findOne, so reading needs no second secret here.
            services.AddSingleton(StrapiReadOptions.FromConfiguration(
                this.startupConfig.Configuration, "Strapi:AdminApiToken"));
            services.AddHttpClient<IFormSpecAdminProvider, StrapiFormSpecAdminProvider>();
            services.AddScoped<IFormSpecAdminService, FormSpecAdminService>();

            // The admin rates editor: an uncached read of the published table, and the
            // write. MyssApi caches the table it serves citizens, so a save reaches
            // them when that cache expires.
            services.AddHttpClient<EligibilityRateTableReader>(
                client => client.Timeout = TimeSpan.FromSeconds(5));
            services.AddTransient<IEligibilityRateProvider, StrapiEligibilityRateProvider>();
            services.AddHttpClient<IEligibilityRateAdminProvider, StrapiEligibilityRateAdminProvider>(
                client => client.Timeout = TimeSpan.FromSeconds(10));
            services.AddScoped<IEligibilityRatesService, EligibilityRatesService>();

            // The worker side, moved from MyssApi: application review over the shared
            // data model (MyssData), stored form submissions and the bus pass PDF.
            // Same connection-string keys and history tables as MyssApi, but this API
            // never migrates: MyssApi's --migrate Job owns the schema, and a model
            // change lands there first.
            IConfiguration configuration = this.startupConfig.Configuration;

            // No database, no startup: the worker screens are nothing without it, and
            // failing here names the missing key instead of a first query failing with
            // a less obvious error.
            string? formsDb = configuration.GetConnectionString("FormsDb");
            if (string.IsNullOrWhiteSpace(formsDb))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:FormsDb is not configured. "
                    + "Locally: appsettings.Development.json points at the compose Postgres. "
                    + "Deployed: set ConnectionStrings__FormsDb from the same secret as MyssApi.");
            }

            services.AddDbContext<FormsDbContext>(options =>
                options.UseNpgsql(formsDb));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("PlatformDb")
                        ?? formsDb,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "platform")));
            services.AddDbContext<IntakeDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("IntakeDb")
                        ?? formsDb,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "intake")));
            services.AddScoped<IEventStore, EventStore>();

            // Specs for the worker screens (the version an application or submission
            // was made on), read with this API's Strapi:AdminApiToken.
            services.AddHttpClient<IFormSpecProvider, StrapiFormSpecProvider>();
            services.AddScoped<IReviewService, ReviewService>();

            // What follows a worker's Accept. Nothing receives applications
            // yet; the placeholder succeeds without leaving the API.
            services.AddSingleton<IApplicationPromotionProvider, PlaceholderApplicationPromotionProvider>();

            services.AddHttpClient<IPdfProvider, CdogsPdfProvider>();
            services.AddSingleton<ITemplateProvider, EmbeddedTemplateProvider>();
            services.AddScoped<IFormSubmissionsService, FormSubmissionsService>();

            // The event log stamps each append with the request's correlation id.
            services.AddHttpContextAccessor();
            services.TryAddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();

            services.TryAddSingleton(TimeProvider.System);

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
