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
        /// Create an initial logger to use during Program startup.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <returns>An instance of a logger.</returns>
        public static ILogger GetInitialLogger(IConfiguration configuration)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateBootstrapLogger();

            using SerilogLoggerFactory factory = new(Log.Logger);
            return factory.CreateLogger("Startup");
        }
    }
}
