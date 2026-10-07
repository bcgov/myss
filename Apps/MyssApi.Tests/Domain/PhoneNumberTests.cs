namespace Myss.Api.Tests.Domain
{
    using Myss.Api.Domain;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="PhoneNumber"/>, driven by the shared vectors so the
    /// browser's <c>phone</c> rule is held to exactly the same set.
    /// </summary>
    public class PhoneNumberTests
    {
        public static IEnumerable<object[]> ValidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Valid("phone"));

        public static IEnumerable<object[]> InvalidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Invalid("phone"));

        [Theory]
        [MemberData(nameof(ValidVectors))]
        public void TryCreate_AcceptsEveryValidVector(string value, string _)
        {
            DomainValidationResult<PhoneNumber> result = PhoneNumber.TryCreate(value);

            Assert.True(result.IsValid, $"Expected \"{value}\" to be accepted but got {result.Keyword}");
            Assert.Equal(10, result.Value!.Digits.Length);
        }

        [Theory]
        [MemberData(nameof(InvalidVectors))]
        public void TryCreate_RejectsEveryInvalidVector_WithTheExpectedKeyword(string value, string expectedKeyword)
        {
            DomainValidationResult<PhoneNumber> result = PhoneNumber.TryCreate(value);

            Assert.False(result.IsValid, $"Expected \"{value}\" to be rejected");
            Assert.Equal(expectedKeyword, result.Keyword);
        }

        [Fact]
        public void TryCreate_DropsTheCountryCode()
        {
            Assert.Equal("2505550199", PhoneNumber.TryCreate("1 (250) 555-0199").Value!.Digits);
        }

        [Fact]
        public void TryCreate_RejectsNull()
        {
            Assert.False(PhoneNumber.TryCreate(null).IsValid);
        }
    }
}
