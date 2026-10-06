namespace Myss.Api.Tests.Domain
{
    using Myss.Api.Domain;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="PostalCode"/>, driven by the shared vectors so the
    /// browser's <c>postalCode</c> rule is held to exactly the same set.
    /// </summary>
    public class PostalCodeTests
    {
        public static IEnumerable<object[]> ValidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Valid("postalCode"));

        public static IEnumerable<object[]> InvalidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Invalid("postalCode"));

        [Theory]
        [MemberData(nameof(ValidVectors))]
        public void TryCreate_AcceptsEveryValidVector(string value, string _)
        {
            DomainValidationResult<PostalCode> result = PostalCode.TryCreate(value);

            Assert.True(result.IsValid, $"Expected \"{value}\" to be accepted but got {result.Keyword}");
            Assert.Equal("V8V 1X4", result.Value!.Value);
        }

        [Theory]
        [MemberData(nameof(InvalidVectors))]
        public void TryCreate_RejectsEveryInvalidVector_WithTheExpectedKeyword(string value, string expectedKeyword)
        {
            DomainValidationResult<PostalCode> result = PostalCode.TryCreate(value);

            Assert.False(result.IsValid, $"Expected \"{value}\" to be rejected");
            Assert.Equal(expectedKeyword, result.Keyword);
        }

        [Fact]
        public void TryCreate_RejectsNull()
        {
            Assert.False(PostalCode.TryCreate(null).IsValid);
        }
    }
}
