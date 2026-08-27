namespace Myss.Api.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// The rate table the browser computes the eligibility estimate against
    /// (Option B — the estimate is not calculated on the server). Served by
    /// GET /v{version}/EligibilityEstimator/rates.
    /// </summary>
    public sealed class EligibilityRatesModel
    {
        /// <summary>Gets the date these values take effect (ISO yyyy-MM-dd).</summary>
        public required string EffectiveDate { get; init; }

        /// <summary>Gets the monthly income-limit rows, one per family size.</summary>
        public required IReadOnlyList<EligibilityRateRowModel> IncomeRows { get; init; }

        /// <summary>Gets the asset ceilings by category.</summary>
        public required EligibilityAssetLimitsModel AssetLimits { get; init; }
    }

    /// <summary>
    /// One family-size row of monthly income limits, by client type A-E
    /// (MYSS-25: A couple/neither, B single/not-PWD, C couple/either,
    /// D single/PWD, E couple/both).
    /// </summary>
    public sealed class EligibilityRateRowModel
    {
        /// <summary>Gets the family unit size (1-7; 7 is the cap).</summary>
        public int FamilySize { get; init; }

        /// <summary>Gets the monthly income limit for client type A.</summary>
        public decimal A { get; init; }

        /// <summary>Gets the monthly income limit for client type B.</summary>
        public decimal B { get; init; }

        /// <summary>Gets the monthly income limit for client type C.</summary>
        public decimal C { get; init; }

        /// <summary>Gets the monthly income limit for client type D.</summary>
        public decimal D { get; init; }

        /// <summary>Gets the monthly income limit for client type E.</summary>
        public decimal E { get; init; }
    }

    /// <summary>
    /// The asset ceilings by category A-D (a separate axis from the income types).
    /// </summary>
    public sealed class EligibilityAssetLimitsModel
    {
        /// <summary>Gets the asset ceiling for category A.</summary>
        public decimal A { get; init; }

        /// <summary>Gets the asset ceiling for category B.</summary>
        public decimal B { get; init; }

        /// <summary>Gets the asset ceiling for category C.</summary>
        public decimal C { get; init; }

        /// <summary>Gets the asset ceiling for category D.</summary>
        public decimal D { get; init; }
    }
}
