namespace Myss.Api.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// Writes eligibility rate tables to the content engine.
    /// </summary>
    public interface IEligibilityRateAdminProvider
    {
        /// <summary>
        /// Saves a complete table as the published table dated <paramref name="effectiveDate"/>:
        /// the table already dated that day is updated, otherwise a new one is created.
        /// Either way the table is published, so the estimator serves it at once.
        /// </summary>
        /// <param name="effectiveDate">The table's effective date (ISO yyyy-MM-dd).</param>
        /// <param name="incomeRows">The income-limit rows, one for each family size.</param>
        /// <param name="assetLimits">The asset ceilings.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The table as the content engine stored it, and whether the save created it.</returns>
        /// <exception cref="StrapiWriteException">The content engine refused the write.</exception>
        /// <exception cref="ContentEngineUnavailableException">
        /// The content engine could not be reached, timed out, refused a read, or answered in an unexpected shape.
        /// </exception>
        Task<SavedEligibilityRates> SaveTableAsync(
            string effectiveDate,
            IReadOnlyList<EligibilityRateRowModel> incomeRows,
            EligibilityAssetLimitsModel assetLimits,
            CancellationToken cancellationToken);
    }

    /// <summary>A saved rate table, and whether the save created it rather than updating it.</summary>
    /// <param name="Rates">The table as the content engine stored it.</param>
    /// <param name="Created">True when no table had the effective date before the save.</param>
    public sealed record SavedEligibilityRates(EligibilityRatesModel Rates, bool Created);
}
