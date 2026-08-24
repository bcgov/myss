namespace Myss.Api.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// An immutable snapshot of the rate data the calculator needs: income limits by
    /// family size and asset ceilings by category. Cached; never mutated.
    /// </summary>
    public sealed class EligibilityRates
    {
        /// <summary>Maximum family size tracked in the rate table (BR-D9-03 / OQ-D9-02).</summary>
        public const int MaxRateTableFamilySize = 7;

        private readonly IReadOnlyDictionary<int, EligibilityRateRow> incomeRates;
        private readonly IReadOnlyDictionary<string, decimal> assetLimits;

        /// <summary>Initializes a new instance of the <see cref="EligibilityRates"/> class.</summary>
        /// <param name="rateRows">Income limit rows, one per family size.</param>
        /// <param name="assetLimits">Asset ceilings by category.</param>
        public EligibilityRates(IEnumerable<EligibilityRateRow> rateRows, IEnumerable<EligibilityAssetLimit> assetLimits)
        {
            ArgumentNullException.ThrowIfNull(rateRows);
            ArgumentNullException.ThrowIfNull(assetLimits);

            // Defensive: if more than one effective date slipped through, the latest wins.
            this.incomeRates = rateRows
                .GroupBy(r => r.FamilySize)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.EffectiveFrom).First());

            this.assetLimits = assetLimits
                .GroupBy(a => a.LimitType)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.EffectiveFrom).First().Limit);
        }

        /// <summary>Returns the monthly income limit, clamping family size to the table cap.</summary>
        /// <param name="clientType">Client type code "A" through "E".</param>
        /// <param name="familySize">The family unit size.</param>
        /// <returns>The monthly income limit.</returns>
        public decimal IncomeLimitFor(string clientType, int familySize)
        {
            var capped = Math.Clamp(familySize, 1, MaxRateTableFamilySize);

            if (!this.incomeRates.TryGetValue(capped, out var row))
            {
                throw new InvalidOperationException($"No rate row seeded for family size {capped}.");
            }

            return row.AmountFor(clientType);
        }

        /// <summary>Returns the asset ceiling for a category.</summary>
        /// <param name="limitType">Category code "A" through "D".</param>
        /// <returns>The maximum total assets permitted.</returns>
        public decimal AssetLimitFor(string limitType)
        {
            if (!this.assetLimits.TryGetValue(limitType, out var limit))
            {
                throw new InvalidOperationException($"No asset limit seeded for category {limitType}.");
            }

            return limit;
        }
    }
}
