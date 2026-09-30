namespace Myss.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Configuration;

    /// <summary>
    /// The entry point for the project.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The entry point for the class.
        /// </summary>
        /// <param name="args">The command line arguments to be passed in.</param>
        public static async Task<int> Main(string[] args)
        {
            if (MigrationRunner.IsRequested(args))
            {
                // Migrate mode: apply the schema and exit. The deployment
                // pipeline runs this as a Job before restarting the API pods.
                using IHost host = ProgramConfiguration.CreateMigrationHostBuilder(
                    MigrationRunner.WithoutSwitch(args)).Build();
                return await MigrationRunner.RunAsync(
                    host.Services.GetRequiredService<IConfiguration>(),
                    host.Services.GetRequiredService<ILoggerFactory>(),
                    CancellationToken.None);
            }

            await CreateHostBuilder(args).Build().RunAsync();
            return 0;
        }

        /// <summary>
        /// Creates the IWebHostBuilder.
        /// </summary>
        /// <param name="args">The command line arguments to be passed in.</param>
        /// <returns>Returns the configured webhost.</returns>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return ProgramConfiguration.CreateHostBuilder<Startup>(args);
        }
    }
}
