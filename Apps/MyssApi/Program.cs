namespace Myss.Api
{
    using Microsoft.Extensions.Hosting;
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
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
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
