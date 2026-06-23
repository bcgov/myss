namespace Myss.Api.Providers
{
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;

    /// <summary>
    /// Provides demo data with hard-coded sample values.
    /// </summary>
    public class DemoProvider : IDemoProvider
    {
        private readonly ILogger<DemoProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DemoProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        public DemoProvider(ILogger<DemoProvider> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public DemoModel GetDemo()
        {
            _logger.LogInformation("Creating demo model with sample data");

            return new DemoModel { Foo = "Sample Foo", Bar = "Sample Bar" };
        }
    }
}
