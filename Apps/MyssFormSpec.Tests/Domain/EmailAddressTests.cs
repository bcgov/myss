namespace Myss.FormSpec.Tests.Domain
{
    using Myss.FormSpec.Domain;
    using Myss.FormSpec.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="EmailAddress"/>, driven by the shared vectors.
    /// </summary>
    public class EmailAddressTests
    {
        public static IEnumerable<object[]> ValidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Valid("email"));

        public static IEnumerable<object[]> InvalidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Invalid("email"));

        public static IEnumerable<object[]> Matching =>
            ValidationVectors.Confirmations("matching").Select(v => new object[] { v.Value, v.Confirmation });

        public static IEnumerable<object[]> Mismatching =>
            ValidationVectors.Confirmations("mismatching").Select(v => new object[] { v.Value, v.Confirmation });

        [Fact]
        public void TryCreate_AnswersAtOnceOnInputThatMadeTheOldPatternBacktrack()
        {
            // "!@!." then many "!." and a character that fails: quadratic for
            // ^[^@\s]+@[^@\s]+\.[^@\s]+$ (CodeQL polynomial ReDoS), linear now.
            string input = "!@!." + string.Concat(Enumerable.Repeat("!.", 50_000)) + "@";
            var watch = System.Diagnostics.Stopwatch.StartNew();

            DomainValidationResult<EmailAddress> result = EmailAddress.TryCreate(input);

            Assert.False(result.IsValid);
            Assert.True(watch.ElapsedMilliseconds < 500, $"Took {watch.ElapsedMilliseconds} ms");
        }

        [Theory]
        [MemberData(nameof(ValidVectors))]
        public void TryCreate_AcceptsEveryValidVector(string value, string _)
        {
            DomainValidationResult<EmailAddress> result = EmailAddress.TryCreate(value);

            Assert.True(result.IsValid, $"Expected \"{value}\" to be accepted but got {result.Keyword}");
        }

        [Theory]
        [MemberData(nameof(InvalidVectors))]
        public void TryCreate_RejectsEveryInvalidVector_WithTheExpectedKeyword(string value, string expectedKeyword)
        {
            DomainValidationResult<EmailAddress> result = EmailAddress.TryCreate(value);

            Assert.False(result.IsValid, $"Expected \"{value}\" to be rejected");
            Assert.Equal(expectedKeyword, result.Keyword);
        }

        [Fact]
        public void TryCreate_TrimsSurroundingWhitespace()
        {
            Assert.Equal("ada@example.com", EmailAddress.TryCreate("  ada@example.com  ").Value!.Value);
        }

        [Fact]
        public void TryCreate_AcceptsPlusAddressing()
        {
            // Rejecting these is a common and hostile validator bug: plus
            // addressing is legitimate and widely used.
            Assert.True(EmailAddress.TryCreate("ada+intake@example.com").IsValid);
        }

        [Theory]
        [MemberData(nameof(Matching))]
        public void ConfirmationMatches_AcceptsEveryMatchingVector(string value, string confirmation)
        {
            Assert.True(EmailAddress.ConfirmationMatches(value, confirmation));
        }

        [Theory]
        [MemberData(nameof(Mismatching))]
        public void ConfirmationMatches_RejectsEveryMismatchingVector(string value, string confirmation)
        {
            Assert.False(EmailAddress.ConfirmationMatches(value, confirmation));
        }

        [Fact]
        public void TheFixtureIsNotEmpty()
        {
            Assert.NotEmpty(ValidationVectors.Valid("email"));
            Assert.NotEmpty(ValidationVectors.Invalid("email"));
            Assert.NotEmpty(ValidationVectors.Confirmations("matching"));
            Assert.NotEmpty(ValidationVectors.Confirmations("mismatching"));
        }
    }
}
