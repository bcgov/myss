namespace Myss.AdminApi.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.AdminApi.Models;
    using Myss.Common.Content;

    /// <summary>
    /// Reads the eligibility rate table from the content engine for the rates
    /// editor. Uncached, so an admin sees a save as soon as it is made.
    /// </summary>
    public interface IEligibilityRateProvider
    {
        /// <summary>
        /// Gets the current published rate table, or the compiled table when the
        /// content engine holds no usable one.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The rate table; never null.</returns>
        /// <exception cref="ContentEngineUnavailableException">
        /// The content engine could not be read: unreachable, timed out, refused the
        /// token, or answered with something unparseable.
        /// </exception>
        Task<EligibilityRatesModel> GetRatesAsync(CancellationToken cancellationToken);
    }
}
