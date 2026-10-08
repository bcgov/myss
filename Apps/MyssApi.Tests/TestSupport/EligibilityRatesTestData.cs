namespace Myss.Api.Tests.TestSupport
{
    using Myss.Api.Data;
    using Myss.Api.Models;

    /// <summary>
    /// The seeded rate table as request and response models, built from the compiled
    /// table (which equals the seed).
    /// </summary>
    public static class EligibilityRatesTestData
    {
        /// <summary>Builds the seven seeded income rows.</summary>
        /// <returns>A new, modifiable list.</returns>
        public static List<EligibilityRateRowModel> SeededRows() =>
        [
            .. FddRateData.RateRows.Select(row => new EligibilityRateRowModel
            {
                FamilySize = row.FamilySize,
                A = row.TypeA,
                B = row.TypeB,
                C = row.TypeC,
                D = row.TypeD,
                E = row.TypeE,
                F = row.TypeF,
                G = row.TypeG,
                H = row.TypeH,
                I = row.TypeI,
            }),
        ];

        /// <summary>Builds the seeded asset limits.</summary>
        /// <returns>The four asset ceilings.</returns>
        public static EligibilityAssetLimitsModel SeededAssetLimits()
        {
            Dictionary<string, decimal> byCategory =
                FddRateData.AssetLimits.ToDictionary(limit => limit.LimitType, limit => limit.Limit);
            return new()
            {
                A = byCategory["A"],
                B = byCategory["B"],
                C = byCategory["C"],
                D = byCategory["D"],
            };
        }

        /// <summary>Builds a save request holding the seeded table.</summary>
        /// <returns>A valid request.</returns>
        public static SaveEligibilityRatesRequestModel SeededRequest() =>
            new() { IncomeRows = SeededRows(), AssetLimits = SeededAssetLimits() };

        /// <summary>Builds the seeded table as the content engine would return it.</summary>
        /// <param name="effectiveDate">The table's effective date.</param>
        /// <returns>The rate table.</returns>
        public static EligibilityRatesModel SeededTable(string effectiveDate) =>
            new() { EffectiveDate = effectiveDate, IncomeRows = SeededRows(), AssetLimits = SeededAssetLimits() };
    }
}
