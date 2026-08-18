namespace Myss.Api.Tests.Domain
{
    using Myss.Api.Domain;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="Sin"/>, driven by the shared vectors.
    /// </summary>
    /// <remarks>
    /// The cases live in <c>Shared/validation/validation-vectors.json</c> rather
    /// than inline, so the TypeScript <c>makeSin</c> built in Phase 1 is held to
    /// exactly the same set. Adding a case there fails both suites until both
    /// handle it, which is the whole point of the arrangement.
    /// </remarks>
    public class SinTests
    {
        public static IEnumerable<object[]> ValidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Valid("sin"));

        public static IEnumerable<object[]> InvalidVectors =>
            ValidationVectors.AsTheoryData(ValidationVectors.Invalid("sin"));

        [Theory]
        [MemberData(nameof(ValidVectors))]
        public void TryCreate_AcceptsEveryValidVector(string value, string _)
        {
            DomainValidationResult<Sin> result = Sin.TryCreate(value);

            Assert.True(result.IsValid, $"Expected \"{value}\" to be accepted but got {result.Keyword}");
            Assert.Equal(9, result.Value!.Digits.Length);
        }

        [Theory]
        [MemberData(nameof(InvalidVectors))]
        public void TryCreate_RejectsEveryInvalidVector_WithTheExpectedKeyword(string value, string expectedKeyword)
        {
            DomainValidationResult<Sin> result = Sin.TryCreate(value);

            Assert.False(result.IsValid, $"Expected \"{value}\" to be rejected");
            Assert.Equal(expectedKeyword, result.Keyword);
        }

        [Fact]
        public void TryCreate_StripsFormattingBeforeChecking()
        {
            // Masking is presentation; the mask must never reach the checksum.
            // A citizen pasting from a document is the normal case, not an edge.
            Assert.Equal(
                Sin.TryCreate("050082833").Value!.Digits,
                Sin.TryCreate("050 082-833").Value!.Digits);
        }

        [Fact]
        public void TryCreate_RejectsNull()
        {
            Assert.False(Sin.TryCreate(null).IsValid);
        }

        [Fact]
        public void ToString_DoesNotLeakTheNumber()
        {
            // Interpolating a Sin into a log line must not print a SIN. This is
            // the cheap half of the PII protection; salted hashing at rest is
            // the other half and is not built yet.
            Sin sin = Sin.TryCreate("050082833").Value!;

            Assert.DoesNotContain("050082833", $"submitted {sin}");
            Assert.Equal("[SIN redacted]", sin.ToString());
        }

        [Fact]
        public void TheFixtureIsNotEmpty()
        {
            // Guards against the suite passing because it silently found no
            // cases to run.
            Assert.NotEmpty(ValidationVectors.Valid("sin"));
            Assert.NotEmpty(ValidationVectors.Invalid("sin"));
        }
    }
}
