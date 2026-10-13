namespace Myss.Api.Domain
{
    /// <summary>
    /// Stable keywords for a refused rate-table save. Apart from
    /// <see cref="ContentEngineRefused"/>, they match MyssContent's
    /// <c>eligibility-rate-rules.ts</c> (<c>RateRule</c>), which applies the same
    /// rules when Strapi stores the table; if one changes there, change it here too.
    /// </summary>
    public static class EligibilityRateKeywords
    {
        /// <summary>The income rows, one of them, or the asset limits are null.</summary>
        public const string ShapeInvalid = "RATES.SHAPE.INVALID";

        /// <summary>The income rows do not hold exactly one row for each family size from 1 to 7.</summary>
        public const string FamilySizesInvalid = "RATES.FAMILY_SIZES.INVALID";

        /// <summary>A couple column holds a value other than 0 at family size 1.</summary>
        public const string NotApplicableNotZero = "RATES.NOT_APPLICABLE.NOT_ZERO";

        /// <summary>An amount is below 0.</summary>
        public const string AmountNegative = "RATES.AMOUNT.NEGATIVE";

        /// <summary>An amount has more than 2 decimal places.</summary>
        public const string AmountTooPrecise = "RATES.AMOUNT.TOO_PRECISE";

        /// <summary>The content engine refused the table by its own rules.</summary>
        public const string ContentEngineRefused = "RATES.CONTENT_ENGINE.REFUSED";
    }
}
