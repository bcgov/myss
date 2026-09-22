namespace Icm.Api.Host
{
    using Icm.Api.Host.Configuration;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// The entry point for the ICM middleware host.
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
        /// Creates the host builder.
        /// </summary>
        /// <param name="args">The command line arguments to be passed in.</param>
        /// <returns>Returns the configured host builder.</returns>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return ProgramConfiguration.CreateHostBuilder<Startup>(args);
        }
    }
}
