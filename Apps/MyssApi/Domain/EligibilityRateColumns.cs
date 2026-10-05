namespace Myss.Api.Domain
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Myss.Api.Models;

    /// <summary>
    /// The rate-table columns, defined once so every check over a rate table
    /// iterates the same list. The letters match MyssContent's <c>eligibility-rate-rules.ts</c>.
    /// </summary>
    public static class EligibilityRateColumns
    {
        /// <summary>Gets the income-limit columns A–I, one per client type.</summary>
        public static IReadOnlyList<EligibilityRateColumn<EligibilityRateRowModel>> Income { get; } =
        [
            new("a", row => row.A),
            new("b", row => row.B),
            new("c", row => row.C),
            new("d", row => row.D),
            new("e", row => row.E),
            new("f", row => row.F),
            new("g", row => row.G),
            new("h", row => row.H),
            new("i", row => row.I),
        ];

        /// <summary>Gets the couple columns, which have no limit at family size 1 and hold 0 there.</summary>
        public static IReadOnlyList<EligibilityRateColumn<EligibilityRateRowModel>> Couple { get; } =
            [.. Income.Where(column => column.Letter is "a" or "c" or "d" or "f" or "h" or "i")];

        /// <summary>Gets the asset-limit columns A–D, one per asset category.</summary>
        public static IReadOnlyList<EligibilityRateColumn<EligibilityAssetLimitsModel>> Asset { get; } =
        [
            new("a", limits => limits.A),
            new("b", limits => limits.B),
            new("c", limits => limits.C),
            new("d", limits => limits.D),
        ];
    }

    /// <summary>One rate-table column.</summary>
    /// <typeparam name="TModel">The model the column's amount is read from.</typeparam>
    /// <param name="Letter">The lower-case letter: the JSON property name and the last part of a field name.</param>
    /// <param name="Amount">Reads the column's amount from the model.</param>
    public sealed record EligibilityRateColumn<TModel>(string Letter, Func<TModel, decimal> Amount);
}
