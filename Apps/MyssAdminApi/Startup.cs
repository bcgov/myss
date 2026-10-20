namespace Myss.AdminApi
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Services;

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

            // Authentication is the Option 1 / Option 2 swap point; the policy and the
            // typed caller below are unaffected by that choice. Every endpoint here
            // requires an IDIR identity (AdminIdir), so there is no role calculation.
            this.startupConfig.ConfigureAuthentication(services);
            services.AddMyssAuthorization();
            services.AddTransient<ICurrentUserAccessor, CurrentUserAccessor>();

            // The admin form editor (MYSS-209). The only Strapi token this API holds is
            // the write-scoped Strapi:AdminApiToken; the citizen API keeps the read-only
            // one. Brokered server-side; the token never reaches the browser.
            services.AddHttpClient<IFormSpecAdminProvider, StrapiFormSpecAdminProvider>();
            services.AddScoped<IFormSpecAdminService, FormSpecAdminService>();

            // The admin rates editor: an uncached read of the published table, and the
            // write. MyssApi caches the table it serves citizens, so a save reaches
            // them when that cache expires.
            services.AddHttpClient<IEligibilityRateProvider, StrapiEligibilityRateProvider>(
                client => client.Timeout = TimeSpan.FromSeconds(5));
            services.AddHttpClient<IEligibilityRateAdminProvider, StrapiEligibilityRateAdminProvider>(
                client => client.Timeout = TimeSpan.FromSeconds(10));
            services.AddScoped<IEligibilityRatesService, EligibilityRatesService>();

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
