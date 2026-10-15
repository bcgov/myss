namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Myss.Api.Domain;
    using Myss.Api.Models;

    /// <summary>
    /// Validates an admin's rate-table save before anything is written. Pure: the
    /// request in, every problem out, each scoped to the cell it concerns
    /// (<c>incomeRows.{familySize}.{letter}</c>, <c>assetLimits.{letter}</c>) or to
    /// the whole list (<c>incomeRows</c>, <c>assetLimits</c>).
    /// </summary>
    /// <remarks>
    /// Mirrors MyssContent's <c>eligibility-rate-rules.ts</c>, which applies the same
    /// rules when Strapi stores the table. A missing column is already refused by
    /// request binding, because every amount on the models is <c>required</c>.
    /// </remarks>
    public static class EligibilityRatesValidator
    {
        /// <summary>The most family sizes a "Found:" list names.</summary>
        private const int FamilySizesShown = 10;

        /// <summary>Validates a save request.</summary>
        /// <param name="request">The table to save.</param>
        /// <returns>Every problem found, in request order. Empty when the table can be saved.</returns>
        public static IReadOnlyList<ValidationErrorModel> Validate(SaveEligibilityRatesRequestModel request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return [.. IncomeRowErrors(request.IncomeRows), .. AssetLimitErrors(request.AssetLimits)];
        }

        // Stops at the first problem with the list itself, so every cell error names
        // a cell that exists exactly once in the table.
        private static IEnumerable<ValidationErrorModel> IncomeRowErrors(IReadOnlyList<EligibilityRateRowModel>? rows)
        {
            if (rows is null)
            {
                yield return Error(
                    "incomeRows",
                    EligibilityRateKeywords.ShapeInvalid,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The income limits must be a list of rows, one for each family size from 1 to {EligibilityRateColumns.MaxFamilySize}."));
                yield break;
            }

            // Binding leaves a JSON null inside the list as a null row.
            if (rows.Any(row => row is null))
            {
                yield return Error("incomeRows", EligibilityRateKeywords.ShapeInvalid, "Each income-limit row must be an object.");
                yield break;
            }

            if (!rows.Select(row => row.FamilySize).Order().SequenceEqual(Enumerable.Range(1, EligibilityRateColumns.MaxFamilySize)))
            {
                yield return Error(
                    "incomeRows",
                    EligibilityRateKeywords.FamilySizesInvalid,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The income limits need exactly one row for each family size from 1 to {EligibilityRateColumns.MaxFamilySize}. Found: {DescribeFamilySizes(rows)}."));
                yield break;
            }

            foreach (EligibilityRateRowModel row in rows)
            {
                foreach (EligibilityRateColumn<EligibilityRateRowModel> column in EligibilityRateColumns.Income)
                {
                    string field = string.Create(CultureInfo.InvariantCulture, $"incomeRows.{row.FamilySize}.{column.Letter}");
                    string place = string.Create(
                        CultureInfo.InvariantCulture,
                        $"Family size {row.FamilySize}, column {column.Letter.ToUpperInvariant()}");
                    decimal amount = column.Amount(row);

                    ValidationErrorModel? problem = AmountProblem(amount, field, place);
                    if (problem is not null)
                    {
                        yield return problem;
                    }
                    else if (row.FamilySize == 1 && amount != 0 && IsCouple(column))
                    {
                        yield return Error(
                            field,
                            EligibilityRateKeywords.NotApplicableNotZero,
                            $"{place} must be 0: it is a couple column, and a couple is never a family of one.");
                    }
                }
            }
        }

        private static IEnumerable<ValidationErrorModel> AssetLimitErrors(EligibilityAssetLimitsModel? limits)
        {
            if (limits is null)
            {
                yield return Error(
                    "assetLimits",
                    EligibilityRateKeywords.ShapeInvalid,
                    "The asset limits must be an object with columns A to D.");
                yield break;
            }

            foreach (EligibilityRateColumn<EligibilityAssetLimitsModel> column in EligibilityRateColumns.Asset)
            {
                ValidationErrorModel? problem = AmountProblem(
                    column.Amount(limits),
                    $"assetLimits.{column.Letter}",
                    $"Asset limit {column.Letter.ToUpperInvariant()}");
                if (problem is not null)
                {
                    yield return problem;
                }
            }
        }

        private static bool IsCouple(EligibilityRateColumn<EligibilityRateRowModel> column) =>
            EligibilityRateColumns.Couple.Any(couple => couple.Letter == column.Letter);

        private static string DescribeFamilySizes(IReadOnlyList<EligibilityRateRowModel> rows)
        {
            if (rows.Count == 0)
            {
                return "none";
            }

            string shown = string.Join(
                ", ",
                rows.Take(FamilySizesShown).Select(row => row.FamilySize.ToString(CultureInfo.InvariantCulture)));
            return rows.Count > FamilySizesShown
                ? string.Create(CultureInfo.InvariantCulture, $"{shown} and {rows.Count - FamilySizesShown} more")
                : shown;
        }

        private static ValidationErrorModel? AmountProblem(decimal amount, string field, string place)
        {
            if (amount < 0)
            {
                return Error(
                    field,
                    EligibilityRateKeywords.AmountNegative,
                    string.Create(CultureInfo.InvariantCulture, $"{place} must be 0 or more, not {amount}."));
            }

            if (amount != decimal.Round(amount, 2))
            {
                return Error(
                    field,
                    EligibilityRateKeywords.AmountTooPrecise,
                    string.Create(CultureInfo.InvariantCulture, $"{place} must have at most 2 decimal places, not {amount}."));
            }

            return null;
        }

        private static ValidationErrorModel Error(string field, string keyword, string message) =>
            new() { Field = field, Keyword = keyword, Message = message };
    }
}
