namespace Myss.Api.Data
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The compiled rate table: the last-resort fallback
    /// <see cref="Myss.Api.Providers.StrapiEligibilityRateProvider"/> serves when the
    /// content engine cannot be read. The values and the effective date must match the
    /// table seeded by MyssContent's <c>eligibility-rate-seed-data.ts</c>.
    /// Ids are fixed because a future EF Core HasData seed needs stable keys.
    /// </summary>
    public static class FddRateData
    {
        /// <summary>The date these values take effect.</summary>
        public static readonly DateOnly EffectiveFrom = new(2026, 10, 2);

        private const string RateNote = "FDD BR-D9-05";

        // Client types (dependants affect family size only; PWD takes priority over 65+):
        //   A = couple, both under 65, neither PWD   B = single, under 65, not PWD
        //   C = couple, both 65+, neither PWD        D = couple, one 65+, neither PWD
        //   E = single, 65+, not PWD                 F = couple, one PWD, other under 65 and not PWD
        //   G = single, PWD                          H = couple, both PWD
        //   I = couple, one PWD and the other 65+
        // Couple columns (A, C, D, F, H, I) have no limit at family size 1 and hold 0.
        private static readonly EligibilityRateRow[] Rates =
        [
            Row(1, 1,    0.00m, 1060.00m,    0.00m,    0.00m, 1360.00m,    0.00m, 1535.50m,    0.00m,    0.00m),
            Row(2, 2, 1650.00m, 1405.00m, 2200.00m, 1950.00m, 1705.00m, 2290.50m, 1880.50m, 2766.00m, 2590.50m),
            Row(3, 3, 1845.00m, 1500.00m, 2395.00m, 2145.00m, 1800.00m, 2485.50m, 1975.50m, 2961.00m, 2785.50m),
            Row(4, 4, 1895.00m, 1550.00m, 2445.00m, 2195.00m, 1850.00m, 2535.50m, 2025.50m, 3011.00m, 2835.50m),
            Row(5, 5, 1945.00m, 1600.00m, 2495.00m, 2245.00m, 1900.00m, 2585.50m, 2075.50m, 3061.00m, 2885.50m),
            Row(6, 6, 1995.00m, 1650.00m, 2545.00m, 2295.00m, 1950.00m, 2635.50m, 2125.50m, 3111.00m, 2935.50m),
            Row(7, 7, 2045.00m, 1700.00m, 2595.00m, 2345.00m, 2000.00m, 2685.50m, 2175.50m, 3161.00m, 2985.50m, RateNote + " (cap)"),
        ];

        private static readonly EligibilityAssetLimit[] Limits =
        [
            Limit(1, "A",   5000.00m, "Asset category A — household mapping pending (MYSS-169 blocker; MYSS-25 does not define asset categories)"),
            Limit(2, "B",  10000.00m, "Asset category B — household mapping pending"),
            Limit(3, "C", 100000.00m, "Asset category C — household mapping pending"),
            Limit(4, "D", 200000.00m, "Asset category D — household mapping pending"),
        ];

        private static readonly EligibilityRates Lookup = new(Rates, Limits);

        /// <summary>Gets the seeded income limit rows.</summary>
        public static IReadOnlyList<EligibilityRateRow> RateRows => Rates;

        /// <summary>Gets the seeded asset limits.</summary>
        public static IReadOnlyList<EligibilityAssetLimit> AssetLimits => Limits;

        /// <summary>Gets the seeded values as a lookup ready for the calculator.</summary>
        public static EligibilityRates Current => Lookup;

        private static EligibilityRateRow Row(
            int id,
            int familySize,
            decimal a,
            decimal b,
            decimal c,
            decimal d,
            decimal e,
            decimal f,
            decimal g,
            decimal h,
            decimal i,
            string notes = RateNote)
            => new()
            {
                Id = id,
                FamilySize = familySize,
                TypeA = a,
                TypeB = b,
                TypeC = c,
                TypeD = d,
                TypeE = e,
                TypeF = f,
                TypeG = g,
                TypeH = h,
                TypeI = i,
                EffectiveFrom = EffectiveFrom,
                Notes = notes,
            };

        private static EligibilityAssetLimit Limit(int id, string limitType, decimal limit, string notes)
            => new()
            {
                Id = id,
                LimitType = limitType,
                Limit = limit,
                EffectiveFrom = EffectiveFrom,
                Notes = notes,
            };
    }
}
