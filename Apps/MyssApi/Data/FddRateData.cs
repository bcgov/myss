namespace Myss.Api.Data
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The August-2023 FDD rate values (BR-D9-05 / BR-D9-06)
    /// <para>
    /// For the estimator POC these compiled values are the single source of truth,
    /// served by <see cref="Myss.Api.Providers.FddRateProvider"/>. Phase D replaces
    /// that provider with a Strapi-backed one so an admin can edit the values without
    /// a deployment; these compiled values then become the last-resort fallback.
    /// Ids are fixed because a future EF Core HasData seed needs stable keys.
    /// </para>
    /// </summary>
    public static class FddRateData
    {
        /// <summary>The date these values take effect.</summary>
        public static readonly DateOnly EffectiveFrom = new(2023, 8, 1);

        private const string RateNote = "FDD BR-D9-05";

        // MYSS-25 (US-ELG-02) rate table. Client-type letters: A = couple neither PWD,
        // B = single not PWD, C = couple either PWD, D = single PWD, E = couple both PWD
        // (dependants feed family size only, never the type). Versus the old FDD table:
        // the family-size-1 row is reassigned to the single types B/D, column C is
        // +165.00 and column E is +113.50 at sizes 2-7. A/B/D at sizes 2-7 are unchanged.
        // See document/MYSS-25-vs-169-EE-Values-Diff.md.
        private static readonly EligibilityRateRow[] Rates =
        [
            Row(1, 1,    0.00m, 1060.00m,    0.00m, 1535.50m,    0.00m),
            Row(2, 2, 1650.00m, 1405.00m, 2290.50m, 1880.50m, 2766.00m),
            Row(3, 3, 1845.00m, 1500.00m, 2485.50m, 1975.50m, 2961.00m),
            Row(4, 4, 1895.00m, 1550.00m, 2535.50m, 2025.50m, 3011.00m),
            Row(5, 5, 1945.00m, 1600.00m, 2585.50m, 2075.50m, 3061.00m),
            Row(6, 6, 1995.00m, 1650.00m, 2635.50m, 2125.50m, 3111.00m),
            Row(7, 7, 2045.00m, 1700.00m, 2685.50m, 2175.50m, 3161.00m, RateNote + " (cap)"),
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
        public static EligibilityRates August2023 => Lookup;

        private static EligibilityRateRow Row(
            int id, int familySize, decimal a, decimal b, decimal c, decimal d, decimal e, string notes = RateNote)
            => new()
            {
                Id = id,
                FamilySize = familySize,
                TypeA = a,
                TypeB = b,
                TypeC = c,
                TypeD = d,
                TypeE = e,
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
