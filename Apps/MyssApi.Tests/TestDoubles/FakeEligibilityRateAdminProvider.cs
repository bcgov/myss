namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Fake <see cref="IEligibilityRateAdminProvider"/> that records each save and
    /// returns the table it was given, or throws a configured exception.
    /// </summary>
    public sealed class FakeEligibilityRateAdminProvider : IEligibilityRateAdminProvider
    {
        /// <summary>Gets every save, in call order.</summary>
        public List<(string EffectiveDate, IReadOnlyList<EligibilityRateRowModel> IncomeRows, EligibilityAssetLimitsModel AssetLimits)> Saves { get; } = [];

        /// <summary>Gets or sets whether a save reports that it created the table.</summary>
        public bool Created { get; set; } = true;

        /// <summary>Gets or sets an exception for <see cref="SaveTableAsync"/> to throw.</summary>
        public Exception? SaveException { get; set; }

        /// <summary>Gets the cancellation token the last save was given.</summary>
        public CancellationToken LastCancellationToken { get; private set; }

        /// <inheritdoc/>
        public Task<SavedEligibilityRates> SaveTableAsync(
            string effectiveDate,
            IReadOnlyList<EligibilityRateRowModel> incomeRows,
            EligibilityAssetLimitsModel assetLimits,
            CancellationToken cancellationToken)
        {
            Saves.Add((effectiveDate, incomeRows, assetLimits));
            LastCancellationToken = cancellationToken;
            if (SaveException is not null)
            {
                throw SaveException;
            }

            EligibilityRatesModel rates = new() { EffectiveDate = effectiveDate, IncomeRows = incomeRows, AssetLimits = assetLimits };
            return Task.FromResult(new SavedEligibilityRates(rates, Created));
        }
    }
}
