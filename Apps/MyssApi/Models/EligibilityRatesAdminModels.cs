namespace Myss.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// An admin's save of the complete rate table. The server assigns the effective
    /// date (today in British Columbia), so the request carries none.
    /// </summary>
    public sealed class SaveEligibilityRatesRequestModel
    {
        /// <summary>Gets the monthly income-limit rows, one for each family size 1-7.</summary>
        public required IReadOnlyList<EligibilityRateRowModel> IncomeRows { get; init; }

        /// <summary>Gets the asset ceilings by category.</summary>
        public required EligibilityAssetLimitsModel AssetLimits { get; init; }
    }

    /// <summary>
    /// The outcome of a rate-table save: the saved table, or the reasons it was refused.
    /// </summary>
    public sealed class EligibilityRatesWriteResultModel
    {
        private EligibilityRatesWriteResultModel()
        {
        }

        /// <summary>Gets the saved table, as published. Null when the save was refused.</summary>
        public EligibilityRatesModel? Rates { get; private init; }

        /// <summary>Gets every reason the save was refused.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; private init; } = [];

        /// <summary>Gets a value indicating whether the save was accepted.</summary>
        [MemberNotNullWhen(true, nameof(Rates))]
        public bool IsValid => Errors.Count == 0;

        /// <summary>Creates an accepted result.</summary>
        /// <param name="rates">The saved table.</param>
        /// <returns>An accepted result.</returns>
        public static EligibilityRatesWriteResultModel Accepted(EligibilityRatesModel rates)
        {
            ArgumentNullException.ThrowIfNull(rates);
            return new() { Rates = rates };
        }

        /// <summary>Creates a refused result.</summary>
        /// <param name="errors">Every reason for refusal; at least one.</param>
        /// <returns>A refused result.</returns>
        public static EligibilityRatesWriteResultModel Refused(IReadOnlyList<ValidationErrorModel> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentOutOfRangeException.ThrowIfZero(errors.Count, nameof(errors));
            return new() { Errors = errors };
        }
    }
}
