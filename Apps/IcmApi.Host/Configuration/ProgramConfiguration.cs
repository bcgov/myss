namespace Icm.Api.Host.Configuration
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using Serilog.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    /// <summary>
    /// Builds the generic host with the configuration precedence and the
    /// logging this middleware uses.
    /// </summary>
    /// <remarks>
    /// Precedence, lowest first: <c>appsettings.json</c>, the environment file,
    /// user secrets (Development only, the store shared with IcmApi.Console),
    /// <c>appsettings.local.json</c>, then environment variables prefixed
    /// <see cref="EnvironmentPrefix"/> (<c>Icm_Icm__BaseUrl</c>,
    /// <c>Icm_Oidc__AllowedClients__0</c>, …), the prefix IcmApi.Console already
    /// uses. Logs are structured JSON on stdout, read from the Serilog section.
    /// </remarks>
    public static class ProgramConfiguration
    {
        /// <summary>The prefix environment variables must carry to be read as settings.</summary>
        public const string EnvironmentPrefix = "Icm_";

        /// <summary>
        /// Creates a host builder with Serilog and the configuration precedence above.
        /// </summary>
        /// <typeparam name="T">The startup class.</typeparam>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The configured host builder.</returns>
        public static IHostBuilder CreateHostBuilder<T>(string[] args)
            where T : class
        {
            return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .UseSerilog((context, logging) => logging
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "IcmApi.Host"))
                .ConfigureAppConfiguration((_, config) =>
                {
                    // Loaded last among files so a local override wins.
                    config.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables(prefix: EnvironmentPrefix);
                })
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<T>());
        }

        /// <summary>
        /// Creates a logger for use during startup, before the host's own is available.
        /// </summary>
        /// <param name="configuration">The configuration to read the Serilog section from.</param>
        /// <returns>A startup logger.</returns>
        public static ILogger GetInitialLogger(IConfiguration configuration)
        {
            Serilog.ILogger logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger();

            using SerilogLoggerFactory factory = new(logger);
            return factory.CreateLogger("Startup");
        }
    }
}
