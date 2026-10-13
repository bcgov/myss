namespace Myss.Api.Tests.Domain
{
    using Myss.Api.Domain;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="Pin"/>, driven by the shared vectors so the
    /// browser's <c>checkPin</c> is held to exactly the same set.
    /// </summary>
    public class PinTests
    {
        public static IEnumerable<object[]> ValidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Valid("pin"));

        public static IEnumerable<object[]> InvalidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Invalid("pin"));

        [Theory]
        [MemberData(nameof(ValidVectors))]
        public void TryCreate_AcceptsEveryValidVector(string value, string _)
        {
            DomainValidationResult<Pin> result = Pin.TryCreate(value);

            Assert.True(result.IsValid, $"Expected \"{value}\" to be accepted but got {result.Keyword}");
            Assert.Equal(value, result.Value!.Digits);
        }

        [Theory]
        [MemberData(nameof(InvalidVectors))]
        public void TryCreate_RejectsEveryInvalidVector_WithTheExpectedKeyword(string value, string expectedKeyword)
        {
            DomainValidationResult<Pin> result = Pin.TryCreate(value);

            Assert.False(result.IsValid, $"Expected \"{value}\" to be rejected");
            Assert.Equal(expectedKeyword, result.Keyword);
        }

        [Fact]
        public void TryCreate_RejectsNull()
        {
            Assert.Equal(ValidationKeywords.PinInvalidFormat, Pin.TryCreate(null).Keyword);
        }

        [Fact]
        public void ToString_NeverShowsTheDigits()
        {
            Assert.DoesNotContain("1234", Pin.TryCreate("1234").Value!.ToString(), StringComparison.Ordinal);
        }
    }
}
