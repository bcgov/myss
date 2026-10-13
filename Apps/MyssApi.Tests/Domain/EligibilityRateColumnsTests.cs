namespace Myss.Api.Tests.Domain
{
    using Myss.Api.Domain;
    using Myss.Api.Models;

    /// <summary>
    /// Tests for <see cref="EligibilityRateColumns"/>.
    /// </summary>
    public class EligibilityRateColumnsTests
    {
        [Fact]
        public void Income_ListsTheNineLettersInOrder()
        {
            Assert.Equal(
                ["a", "b", "c", "d", "e", "f", "g", "h", "i"],
                EligibilityRateColumns.Income.Select(column => column.Letter));
        }

        [Fact]
        public void Couple_ListsTheTwoAdultColumns()
        {
            Assert.Equal(
                ["a", "c", "d", "f", "h", "i"],
                EligibilityRateColumns.Couple.Select(column => column.Letter));
        }

        [Fact]
        public void Asset_ListsTheFourCategories()
        {
            Assert.Equal(
                ["a", "b", "c", "d"],
                EligibilityRateColumns.Asset.Select(column => column.Letter));
        }

        [Fact]
        public void Income_EachColumnReadsItsOwnProperty()
        {
            var row = new EligibilityRateRowModel
            {
                FamilySize = 2,
                A = 1,
                B = 2,
                C = 3,
                D = 4,
                E = 5,
                F = 6,
                G = 7,
                H = 8,
                I = 9,
            };

            Assert.Equal(
                [1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m],
                EligibilityRateColumns.Income.Select(column => column.Amount(row)));
        }

        [Fact]
        public void Asset_EachColumnReadsItsOwnProperty()
        {
            var limits = new EligibilityAssetLimitsModel { A = 1, B = 2, C = 3, D = 4 };

            Assert.Equal(
                [1m, 2m, 3m, 4m],
                EligibilityRateColumns.Asset.Select(column => column.Amount(limits)));
        }
    }
}
