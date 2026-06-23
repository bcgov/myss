namespace Myss.Api.Providers
{
    using Myss.Api.Models;

    /// <summary>
    /// Interface for a provider that supplies demo data.
    /// </summary>
    public interface IDemoProvider
    {
        /// <summary>
        /// Creates a demo model populated with sample data.
        /// </summary>
        /// <returns>A <see cref="DemoModel"/> containing sample data.</returns>
        DemoModel GetDemo();
    }
}
