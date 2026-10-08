namespace Myss.Api.Tests.Services
{
    using System.Globalization;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using static Myss.Api.Tests.TestSupport.EligibilityRatesTestData;

    /// <summary>
    /// Tests for <see cref="EligibilityRatesValidator"/>.
    /// </summary>
    public class EligibilityRatesValidatorTests
    {
        [Fact]
        public void TheSeededTable_IsValid()
        {
            Assert.Empty(EligibilityRatesValidator.Validate(Request(SeededRows())));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1880.5")]
        [InlineData("1880.50")]
        [InlineData("1880.500")] // trailing zeros keep the scale they were written with
        public void AnAmountWithAtMostTwoSignificantDecimals_IsValid(string amount)
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[1] = WithAmount(rows[1], "g", Parse(amount));

            Assert.Empty(EligibilityRatesValidator.Validate(Request(rows)));
        }

        [Theory]
        [InlineData("0.001")]
        [InlineData("1535.555")]
        [InlineData("0.0000000000000000000000000001")]
        public void AnAmountWithMoreThanTwoDecimals_IsTooPrecise(string amount)
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[0] = WithAmount(rows[0], "g", Parse(amount));

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal(("incomeRows.1.g", EligibilityRateKeywords.AmountTooPrecise), (error.Field, error.Keyword));
        }

        [Fact]
        public void AMissingFamilySize_IsReportedOnTheIncomeRows()
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows.RemoveAt(6);

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal("incomeRows", error.Field);
            Assert.Equal(EligibilityRateKeywords.FamilySizesInvalid, error.Keyword);
            Assert.Equal(
                "The income limits need exactly one row for each family size from 1 to 7. Found: 1, 2, 3, 4, 5, 6.",
                error.Message);
        }

        [Fact]
        public void ADuplicatedFamilySize_IsReported()
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[6] = WithFamilySize(rows[6], 6);

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal(("incomeRows", EligibilityRateKeywords.FamilySizesInvalid), (error.Field, error.Keyword));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(8)]
        public void AFamilySizeOutsideOneToSeven_IsReported(int familySize)
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[6] = WithFamilySize(rows[6], familySize);

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal(("incomeRows", EligibilityRateKeywords.FamilySizesInvalid), (error.Field, error.Keyword));
        }

        [Fact]
        public void WrongFamilySizes_AreReportedWithoutCellErrors()
        {
            // A cell error would name a family size that is missing or appears twice.
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[6] = WithAmount(WithFamilySize(rows[6], 8), "a", -1m);

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal(EligibilityRateKeywords.FamilySizesInvalid, error.Keyword);
        }

        [Fact]
        public void ALongListOfFamilySizes_IsShortenedInTheMessage()
        {
            List<EligibilityRateRowModel> rows = [.. SeededRows(), .. SeededRows()];

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.EndsWith("Found: 1, 2, 3, 4, 5, 6, 7, 1, 2, 3 and 4 more.", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void NoIncomeRows_ReportsTheFamilySizes()
        {
            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request([])));

            Assert.Equal(EligibilityRateKeywords.FamilySizesInvalid, error.Keyword);
            Assert.EndsWith("Found: none.", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("c")]
        [InlineData("d")]
        [InlineData("f")]
        [InlineData("h")]
        [InlineData("i")]
        public void ACoupleColumnAtFamilySizeOne_MustBeZero(string letter)
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[0] = WithAmount(rows[0], letter, 1m);

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal($"incomeRows.1.{letter}", error.Field);
            Assert.Equal(EligibilityRateKeywords.NotApplicableNotZero, error.Keyword);
            Assert.StartsWith(
                $"Family size 1, column {letter.ToUpperInvariant()} must be 0",
                error.Message,
                StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("b")]
        [InlineData("c")]
        [InlineData("d")]
        [InlineData("e")]
        [InlineData("f")]
        [InlineData("g")]
        [InlineData("h")]
        [InlineData("i")]
        public void ANegativeIncomeAmount_IsReportedOnItsCell(string letter)
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[1] = WithAmount(rows[1], letter, -0.01m);

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal($"incomeRows.2.{letter}", error.Field);
            Assert.Equal(EligibilityRateKeywords.AmountNegative, error.Keyword);
            Assert.Equal(
                $"Family size 2, column {letter.ToUpperInvariant()} must be 0 or more, not -0.01.",
                error.Message);
        }

        [Fact]
        public void ANegativeCoupleAmountAtFamilySizeOne_IsReportedOnce()
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[0] = WithAmount(rows[0], "a", -1m);

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal(("incomeRows.1.a", EligibilityRateKeywords.AmountNegative), (error.Field, error.Keyword));
        }

        [Theory]
        [InlineData("a")]
        [InlineData("b")]
        [InlineData("c")]
        [InlineData("d")]
        public void AnAssetAmountWithThreeDecimals_IsReportedOnItsCell(string letter)
        {
            EligibilityAssetLimitsModel limits = WithAssetAmount(SeededAssetLimits(), letter, 1535.555m);

            ValidationErrorModel error = Assert.Single(
                EligibilityRatesValidator.Validate(Request(SeededRows(), limits)));

            Assert.Equal($"assetLimits.{letter}", error.Field);
            Assert.Equal(EligibilityRateKeywords.AmountTooPrecise, error.Keyword);
            Assert.Equal(
                $"Asset limit {letter.ToUpperInvariant()} must have at most 2 decimal places, not 1535.555.",
                error.Message);
        }

        [Fact]
        public void ANegativeAssetAmount_IsReported()
        {
            EligibilityAssetLimitsModel limits = WithAssetAmount(SeededAssetLimits(), "b", -5m);

            ValidationErrorModel error = Assert.Single(
                EligibilityRatesValidator.Validate(Request(SeededRows(), limits)));

            Assert.Equal(("assetLimits.b", EligibilityRateKeywords.AmountNegative), (error.Field, error.Keyword));
        }

        [Fact]
        public void SeveralProblems_GiveOneErrorEach_InTableOrder()
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[0] = WithAmount(rows[0], "a", 1m);
            rows[1] = WithAmount(rows[1], "b", -0.01m);
            rows[2] = WithAmount(rows[2], "i", 2785.555m);
            EligibilityAssetLimitsModel limits = WithAssetAmount(SeededAssetLimits(), "c", 1535.555m);

            IReadOnlyList<ValidationErrorModel> errors = EligibilityRatesValidator.Validate(Request(rows, limits));

            Assert.Equal(
                ["incomeRows.1.a", "incomeRows.2.b", "incomeRows.3.i", "assetLimits.c"],
                errors.Select(error => error.Field));
        }

        [Fact]
        public void ANullRow_IsReportedOnce_WithoutFailing()
        {
            List<EligibilityRateRowModel> rows = SeededRows();
            rows[3] = null!;

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(Request(rows)));

            Assert.Equal(("incomeRows", EligibilityRateKeywords.ShapeInvalid), (error.Field, error.Keyword));
        }

        [Fact]
        public void NullIncomeRows_AreReportedWithoutFailing()
        {
            SaveEligibilityRatesRequestModel request = new() { IncomeRows = null!, AssetLimits = SeededAssetLimits() };

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(request));

            Assert.Equal(("incomeRows", EligibilityRateKeywords.ShapeInvalid), (error.Field, error.Keyword));
        }

        [Fact]
        public void NullAssetLimits_AreReportedWithoutFailing()
        {
            SaveEligibilityRatesRequestModel request = new() { IncomeRows = SeededRows(), AssetLimits = null! };

            ValidationErrorModel error = Assert.Single(EligibilityRatesValidator.Validate(request));

            Assert.Equal(("assetLimits", EligibilityRateKeywords.ShapeInvalid), (error.Field, error.Keyword));
        }

        [Fact]
        public void Messages_UseInvariantNumbers_WhateverTheCurrentCulture()
        {
            // Swedish writes a decimal comma and, with ICU, the Unicode minus sign.
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            try
            {
                List<EligibilityRateRowModel> negative = SeededRows();
                negative[1] = WithAmount(negative[1], "b", -0.01m);
                ValidationErrorModel amountError = Assert.Single(EligibilityRatesValidator.Validate(Request(negative)));

                List<EligibilityRateRowModel> sizes = SeededRows();
                sizes[6] = WithFamilySize(sizes[6], -1);
                ValidationErrorModel sizeError = Assert.Single(EligibilityRatesValidator.Validate(Request(sizes)));

                Assert.Equal("Family size 2, column B must be 0 or more, not -0.01.", amountError.Message);
                Assert.EndsWith("Found: 1, 2, 3, 4, 5, 6, -1.", sizeError.Message, StringComparison.Ordinal);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static decimal Parse(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);

        private static SaveEligibilityRatesRequestModel Request(
            List<EligibilityRateRowModel> rows,
            EligibilityAssetLimitsModel? assetLimits = null) =>
            new() { IncomeRows = rows, AssetLimits = assetLimits ?? SeededAssetLimits() };

        private static EligibilityRateRowModel WithAmount(EligibilityRateRowModel row, string letter, decimal amount)
        {
            if (!EligibilityRateColumns.Income.Any(column => column.Letter == letter))
            {
                throw new ArgumentOutOfRangeException(nameof(letter), letter, "Not an income column.");
            }

            return new()
            {
                FamilySize = row.FamilySize,
                A = letter == "a" ? amount : row.A,
                B = letter == "b" ? amount : row.B,
                C = letter == "c" ? amount : row.C,
                D = letter == "d" ? amount : row.D,
                E = letter == "e" ? amount : row.E,
                F = letter == "f" ? amount : row.F,
                G = letter == "g" ? amount : row.G,
                H = letter == "h" ? amount : row.H,
                I = letter == "i" ? amount : row.I,
            };
        }

        private static EligibilityRateRowModel WithFamilySize(EligibilityRateRowModel row, int familySize) =>
            new()
            {
                FamilySize = familySize,
                A = row.A,
                B = row.B,
                C = row.C,
                D = row.D,
                E = row.E,
                F = row.F,
                G = row.G,
                H = row.H,
                I = row.I,
            };

        private static EligibilityAssetLimitsModel WithAssetAmount(
            EligibilityAssetLimitsModel limits,
            string letter,
            decimal amount)
        {
            if (!EligibilityRateColumns.Asset.Any(column => column.Letter == letter))
            {
                throw new ArgumentOutOfRangeException(nameof(letter), letter, "Not an asset column.");
            }

            return new()
            {
                A = letter == "a" ? amount : limits.A,
                B = letter == "b" ? amount : limits.B,
                C = letter == "c" ? amount : limits.C,
                D = letter == "d" ? amount : limits.D,
            };
        }
    }
}
