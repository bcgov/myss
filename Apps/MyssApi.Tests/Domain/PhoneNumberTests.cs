namespace Myss.Api.Tests.Domain
{
    using Myss.Api.Domain;
    using Myss.Api.Tests.TestSupport;

    public class PhoneNumberTests
    {
        public static IEnumerable<object[]> ValidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Valid("phone"));

        public static IEnumerable<object[]> InvalidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Invalid("phone"));

        [Theory]
        [MemberData(nameof(ValidVectors))]
        public void TryCreate_AcceptsValidPhoneNumbers(string value, string _)
        {
            DomainValidationResult<PhoneNumber> result = PhoneNumber.TryCreate(value);

            Assert.True(result.IsValid, $"Expected \"{value}\" to be accepted but got {result.Keyword}");
            Assert.Equal(10, result.Value!.Digits.Length);
        }

        [Theory]
        [MemberData(nameof(InvalidVectors))]
        public void TryCreate_RejectsInvalidPhoneNumbers(string value, string expectedKeyword)
        {
            DomainValidationResult<PhoneNumber> result = PhoneNumber.TryCreate(value);

            Assert.False(result.IsValid, $"Expected \"{value}\" to be rejected");
            Assert.Equal(expectedKeyword, result.Keyword);
            Assert.Equal("Phone number is invalid", result.Message);
        }

        [Fact]
        public void TryCreate_RejectsNull()
        {
            Assert.False(PhoneNumber.TryCreate(null).IsValid);
        }

        [Fact]
        public void ToString_DoesNotLeakPhoneNumber()
        {
            PhoneNumber phone = PhoneNumber.TryCreate("2505550199").Value!;

            Assert.DoesNotContain("2505550199", phone.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheFixtureIsNotEmpty()
        {
            Assert.NotEmpty(ValidationVectors.Valid("phone"));
            Assert.NotEmpty(ValidationVectors.Invalid("phone"));
        }
    }
}