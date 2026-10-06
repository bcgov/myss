namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Fake <see cref="IEligibilityRateProvider"/> that serves the seeded table and
    /// counts cache evictions.
    /// </summary>
    public sealed class FakeEligibilityRateProvider : IEligibilityRateProvider
    {
        /// <summary>Gets how many times <see cref="InvalidateCache"/> was called.</summary>
        public int InvalidateCacheCalls { get; private set; }

        /// <inheritdoc/>
        public Task<EligibilityRatesModel> GetRatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(EligibilityRatesTestData.SeededTable("2026-10-02"));

        /// <inheritdoc/>
        public void InvalidateCache() => InvalidateCacheCalls++;
    }
}
