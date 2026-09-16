namespace Icm.Api.Host
{
    using Icm.Api.Host.Configuration;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Configures the middleware host during startup.
    /// </summary>
    /// <remarks>
    /// The host is deliberately small: one authenticated route in MySS business
    /// language, the ICM client library behind it, and nothing Siebel-shaped in
    /// between. MyssApi is the only intended caller; see
    /// <see cref="StartupConfiguration.ConfigureAuthentication"/> for how that is
    /// enforced.
    /// </remarks>
    public class Startup
    {
        private readonly StartupConfiguration startupConfig;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="env">The injected environment provider.</param>
        /// <param name="configuration">The injected configuration provider.</param>
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            this.startupConfig = new StartupConfiguration(configuration, env);
        }

        /// <summary>
        /// Adds services to the container.
        /// </summary>
        /// <param name="services">The injected services provider.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            this.startupConfig.ConfigureHttpServices(services);
            this.startupConfig.ConfigureSwaggerServices(services);
            this.startupConfig.ConfigureAuthentication(services);
            this.startupConfig.ConfigureAuthorization(services);

            // The ICM client library and the submitter over it. Fail-closed on the
            // non-secret settings, like MyssApi: a host that cannot say where ICM is
            // must not start.
            services.AddIcmClient(this.startupConfig.Configuration, this.startupConfig.Logger);
        }

        /// <summary>
        /// Configures the HTTP request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        public void Configure(IApplicationBuilder app)
        {
            this.startupConfig.UseHttp(app);
        }
    }
}
