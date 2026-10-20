namespace Myss.AdminApi
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Myss.AdminApi.Configuration;

    /// <summary>
    /// The entry point for the project.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The entry point for the class.
        /// </summary>
        /// <param name="args">The command line arguments to be passed in.</param>
        /// <returns>A task that completes when the host shuts down.</returns>
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
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
