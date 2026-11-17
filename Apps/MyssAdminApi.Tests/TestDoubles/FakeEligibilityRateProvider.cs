namespace Myss.AdminApi.Tests.TestDoubles
{
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Tests.TestSupport;
    using Myss.Common.Content;

    /// <summary>
    /// Fake <see cref="IEligibilityRateProvider"/> that serves the seeded table and
    /// counts reads.
    /// </summary>
    public sealed class FakeEligibilityRateProvider : IEligibilityRateProvider
    {
        /// <summary>Gets how many times <see cref="GetRatesAsync"/> was called.</summary>
        public int GetRatesCalls { get; private set; }

        /// <inheritdoc/>
        public Task<EligibilityRatesModel> GetRatesAsync(CancellationToken cancellationToken)
        {
            GetRatesCalls++;
            return Task.FromResult(EligibilityRatesTestData.SeededTable("2026-10-02"));
        }
    }
}
