namespace Myss.Api.Tests.Domain
{
    using Myss.Api.Domain;

    /// <summary>
    /// Tests for <see cref="PhoneNumber"/>. Every number is in the 555-01xx
    /// range reserved for fiction.
    /// </summary>
    public class PhoneNumberTests
    {
        [Theory]
        [InlineData("(250) 555-0123")]
        [InlineData("250-555-0123")]
        [InlineData("250.555.0123")]
        [InlineData("2505550123")]
        [InlineData(" 250 555 0123 ")]
        public void TryCreate_AcceptsTheUsualWaysOfWritingANumber(string value)
        {
            DomainValidationResult<PhoneNumber> result = PhoneNumber.TryCreate(value);

            Assert.True(result.IsValid, $"Expected \"{value}\" to be accepted but got {result.Keyword}");
            Assert.Equal("2505550123", result.Value!.Digits);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("555-0123")]
        [InlineData("1 250 555 0123")]
        [InlineData("250-555-01234")]
        [InlineData("250-555-0123 ext 4")]
        [InlineData("+1 250 555 0123")]
        [InlineData("250-CALL-NOW")]
        public void TryCreate_RefusesAnythingButTenDigits(string? value)
        {
            DomainValidationResult<PhoneNumber> result = PhoneNumber.TryCreate(value);

            Assert.False(result.IsValid, $"Expected \"{value}\" to be rejected");
            Assert.Equal(ValidationKeywords.PhoneInvalidFormat, result.Keyword);
        }
    }
}
