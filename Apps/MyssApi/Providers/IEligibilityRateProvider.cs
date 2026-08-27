namespace Myss.Api.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// Provides the eligibility rate table from the content engine, with a
    /// compiled fallback. The browser computes the estimate against it; the
    /// provider never calculates.
    /// </summary>
    public interface IEligibilityRateProvider
    {
        /// <summary>
        /// Gets the current published rate table, falling back to the compiled
        /// MYSS-25 values when the content engine cannot be read.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The rate table; never null.</returns>
        Task<EligibilityRatesModel> GetRatesAsync(CancellationToken cancellationToken);
    }
}
