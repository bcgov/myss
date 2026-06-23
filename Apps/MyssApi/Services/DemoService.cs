namespace Myss.Api.Services
{
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Service that provides demo data.
    /// </summary>
    public class DemoService : IDemoService
    {
        private readonly IDemoProvider _demoProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DemoService"/> class.
        /// </summary>
        /// <param name="demoProvider">Injected Demo Provider.</param>
        public DemoService(IDemoProvider demoProvider)
        {
            _demoProvider = demoProvider;
        }

        /// <inheritdoc/>
        public DemoModel GetDemo()
        {
            return _demoProvider.GetDemo();
        }
    }
}
