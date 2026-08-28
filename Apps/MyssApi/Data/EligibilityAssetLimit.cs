namespace Myss.Api.Data
{
    using System;

    /// <summary>
    /// The asset ceiling for one asset-limit category (BR-D9-06).
    /// </summary>
    public class EligibilityAssetLimit
    {
        /// <summary>Gets or sets the surrogate key.</summary>
        public int Id { get; set; }

        /// <summary>Gets or sets the category code "A" through "D".</summary>
        public string LimitType { get; set; } = string.Empty;

        /// <summary>Gets or sets the maximum total assets permitted.</summary>
        public decimal Limit { get; set; }

        /// <summary>Gets or sets the date this limit takes effect.</summary>
        public DateOnly EffectiveFrom { get; set; }

        /// <summary>Gets or sets provenance notes.</summary>
        public string? Notes { get; set; }
    }
}
