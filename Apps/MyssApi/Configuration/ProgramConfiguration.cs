namespace Myss.Api.Configuration
{
    using Myss.Api.Configuration.Addons.Observability;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using Serilog.Events;
    using Serilog.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    /// <summary>
    /// Static class that provides configuration methods for a c# program.
    /// </summary>
    public static class ProgramConfiguration
    {
        private const string EnvironmentPrefix = "Myss_";

        /// <summary>
        /// Creates a IHostBuilder with console logging and Configuration prefixing enabled.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>Returns the configured WebHostBuilder.</returns>
        public static IHostBuilder CreateHostBuilder<T>(string[] args)
            where T : class
        {
            return Host.CreateDefaultBuilder(args)
                .UseDefaultLogging()
                .ConfigureAppConfiguration(
                    (_, config) =>
                    {
                        // Loads local settings last to keep override
                        config.AddJsonFile("appsettings.local.json", true, true);
                        config.AddEnvironmentVariables(prefix: EnvironmentPrefix);
                    }
                )
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<T>());
        }

        /// <summary>
        /// The host for migrate mode: the same configuration layering and
        /// logging as the API, without the web host or <see cref="Startup"/>,
        /// so it boots with nothing but connection strings configured.
        /// </summary>
        /// <param name="args">Command-line arguments, without the migrate switch.</param>
        /// <returns>The host builder.</returns>
        public static IHostBuilder CreateMigrationHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                // The generic host reads DOTNET_ENVIRONMENT only; the web host
                // adds this prefix so ASPNETCORE_ENVIRONMENT selects the
                // environment. Same here, so the Job and the API pods agree
                // on which appsettings file applies.
                .ConfigureHostConfiguration(config => config.AddEnvironmentVariables(prefix: "ASPNETCORE_"))
                .UseDefaultLogging()
                .ConfigureAppConfiguration(
                    (_, config) =>
                    {
                        config.AddJsonFile("appsettings.local.json", true, true);
                        config.AddEnvironmentVariables(prefix: EnvironmentPrefix);
                    });
        }

        /// <summary>
        /// Create an initial logger to use during Program startup.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <returns>An instance of a logger.</returns>
        public static ILogger GetInitialLogger(IConfiguration configuration)
        {
            Serilog.ILogger logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger();

            using SerilogLoggerFactory factory = new(logger);
            return factory.CreateLogger("Startup");
        }
    }
}
