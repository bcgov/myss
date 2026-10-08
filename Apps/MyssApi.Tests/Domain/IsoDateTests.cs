namespace Myss.Api.Tests.Domain
{
    using Myss.Api.Domain;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="IsoDate"/>, driven by the shared vectors so the
    /// browser's <c>date</c> rule is held to exactly the same set.
    /// </summary>
    public class IsoDateTests
    {
        public static IEnumerable<object[]> ValidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Valid("date"));

        public static IEnumerable<object[]> InvalidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Invalid("date"));

        [Theory]
        [MemberData(nameof(ValidVectors))]
        public void TryParse_AcceptsEveryValidVector(string value, string _)
        {
            Assert.True(IsoDate.TryParse(value, out DateOnly date), $"Expected \"{value}\" to parse");
            Assert.NotEqual(default, date);
        }

        [Theory]
        [MemberData(nameof(InvalidVectors))]
        public void TryParse_RejectsEveryInvalidVector(string value, string expectedKeyword)
        {
            Assert.False(IsoDate.TryParse(value, out _), $"Expected \"{value}\" to be rejected");
            Assert.Equal("FORM.DATE.INVALID", expectedKeyword);
        }

        [Fact]
        public void TryParse_ReadsOnlyTheDatePortionOfADatetime()
        {
            Assert.True(IsoDate.TryParse("2024-01-01T08:00:00-08:00", out DateOnly date));
            Assert.Equal(new DateOnly(2024, 1, 1), date);
        }

        [Theory]
        [InlineData(2000, 2, 29, true)]
        [InlineData(2001, 2, 29, false)]
        [InlineData(2024, 4, 31, false)]
        [InlineData(2024, 13, 1, false)]
        [InlineData(0, 1, 1, false)]
        public void TryCreate_KnowsTheCalendar(int year, int month, int day, bool real)
        {
            Assert.Equal(real, IsoDate.TryCreate(year, month, day, out _));
        }
    }
}
