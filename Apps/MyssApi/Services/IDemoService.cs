namespace Myss.Api.Services
{
    using Myss.Api.Models;

    /// <summary>
    /// Interface for a service that provides demo data.
    /// </summary>
    public interface IDemoService
    {
        /// <summary>
        /// Gets a demo model populated with sample data.
        /// </summary>
        /// <returns>A <see cref="DemoModel"/> containing sample data.</returns>
        DemoModel GetDemo();
    }
}
