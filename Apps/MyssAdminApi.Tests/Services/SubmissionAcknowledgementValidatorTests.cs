namespace Myss.AdminApi.Tests.Services
{
    using System.Globalization;
    using Myss.AdminApi.Domain;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Services;
    using Myss.FormSpec;

    /// <summary>
    /// Tests for <see cref="SubmissionAcknowledgementValidator"/>. MyssWebCommon's
    /// <c>acknowledgements.unit.test.ts</c> checks the same cases against the same wording.
    /// </summary>
    public class SubmissionAcknowledgementValidatorTests
    {
        private const string Placeholder = "{referenceNumber}";

        private const string BuiltInBody =
            "The BC Bus Pass Program has received your request. Your reference number is {referenceNumber}. Keep it for your records.\n"
            + "We may contact you if we need further information for your request.";

        [Fact]
        public void TheBuiltInBusPassMessage_IsValid()
        {
            Assert.Empty(Validate("Bus Pass Request is complete", "Request submitted", BuiltInBody));
        }

        [Theory]
        [InlineData("heading", "Enter a page heading.")]
        [InlineData("title", "Enter a message title.")]
        [InlineData("body", "Enter the message.")]
        public void AnEmptyText_IsRequired(string field, string message)
        {
            foreach (string? blank in new[] { null, string.Empty, "   ", "\r\n\t" })
            {
                ValidationErrorModel error = Assert.Single(Validate(With(field, blank)));

                Assert.Equal(field, error.Field);
                Assert.Equal(SubmissionAcknowledgementKeywords.TextRequired, error.Keyword);
                Assert.Equal(message, error.Message);
            }
        }

        [Theory]
        [InlineData("heading", 100, "The page heading")]
        [InlineData("title", 100, "The message title")]
        [InlineData("body", 1000, "The message")]
        public void EachText_IsLimited(string field, int limit, string label)
        {
            Assert.Empty(Validate(With(field, TextOfLength(field, limit))));

            ValidationErrorModel error = Assert.Single(Validate(With(field, TextOfLength(field, limit + 1))));
            Assert.Equal(field, error.Field);
            Assert.Equal(SubmissionAcknowledgementKeywords.TextTooLong, error.Keyword);
            Assert.Equal(
                string.Create(CultureInfo.InvariantCulture, $"{label} must be {limit:N0} characters or fewer. It has {limit + 1:N0}."),
                error.Message);
        }

        [Fact]
        public void TheLengthLimit_CountsALineBreakOnce_AndNotTheSpacesAtEitherEnd()
        {
            string body = Placeholder + "\r\n" + new string('x', 1000 - Placeholder.Length - 1);

            Assert.Equal(1001, body.Length);
            Assert.Empty(Validate(With("body", "  " + body + "\r\n  ")));
        }

        [Fact]
        public void TheOverLengthMessage_GivesTheCountInFull()
        {
            ValidationErrorModel error = Assert.Single(Validate(With("body", TextOfLength("body", 1012))));

            Assert.Equal("The message must be 1,000 characters or fewer. It has 1,012.", error.Message);
        }

        [Fact]
        public void AMessageWithoutThePlaceholder_IsRefused()
        {
            ValidationErrorModel error = Assert.Single(Validate(With("body", "Your reference number is on its way.")));

            Assert.Equal("body", error.Field);
            Assert.Equal(SubmissionAcknowledgementKeywords.ReferenceMissing, error.Keyword);
            Assert.Equal("Add {referenceNumber} where the applicant's reference number should appear.", error.Message);
        }

        [Theory]
        [InlineData("[track it](https://www2.gov.bc.ca/?r={referenceNumber})")]
        [InlineData("[{referenceNumber}](https://www2.gov.bc.ca)")]
        public void APlaceholderInsideALink_DoesNotCount(string link)
        {
            ValidationErrorModel error = Assert.Single(Validate(With("body", $"See {link}.")));

            Assert.Equal(SubmissionAcknowledgementKeywords.ReferenceMissing, error.Keyword);
        }

        [Fact]
        public void AnOverLengthMessage_IsNotCheckedFurther()
        {
            ValidationErrorModel error = Assert.Single(Validate(With("body", new string('[', 1001))));

            Assert.Equal(SubmissionAcknowledgementKeywords.TextTooLong, error.Keyword);
        }

        [Fact]
        public void AMisspeltPlaceholder_IsUnknown_AndThePlaceholderIsMissing()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate(With("body", "Your number is {reference}. Again: {reference}."));

            Assert.Equal(
                [SubmissionAcknowledgementKeywords.ReferenceMissing, SubmissionAcknowledgementKeywords.PlaceholderUnknown],
                errors.Select(error => error.Keyword));
            Assert.Equal(
                "{reference} is not a placeholder this message can use. The only one is {referenceNumber}.",
                errors[1].Message);
        }

        [Theory]
        [InlineData("[the program](https://www2.gov.bc.ca/gov/content/bus-pass)")]
        [InlineData("[email us](mailto:buspass@example.com)")]
        [InlineData("[call us](tel:18005551234)")]
        [InlineData("[shouting](HTTPS://www2.gov.bc.ca)")]
        public void ALinkToHttpsMailOrPhone_IsAccepted(string link)
        {
            Assert.Empty(Validate(With("body", $"See {link}. {Placeholder}")));
        }

        [Theory]
        [InlineData("[the program](http://www2.gov.bc.ca)")]
        [InlineData("[the program](javascript:alert(1)")]
        [InlineData("[the program](/buspass)")]
        [InlineData("[the program]()")]
        [InlineData("[the program](https://)")]
        [InlineData("[the program](https://www2.gov.bc.ca and more)")]
        [InlineData("[the program](tel:1 800 555 1234)")]
        public void AnyOtherLink_IsRefused(string link)
        {
            ValidationErrorModel error = Assert.Single(Validate(With("body", $"See {link}. {Placeholder}")));

            Assert.Equal("body", error.Field);
            Assert.Equal(SubmissionAcknowledgementKeywords.LinkInvalid, error.Keyword);
            Assert.Equal("The link “the program” must start with https://, mailto: or tel:, with no spaces.", error.Message);
        }

        [Fact]
        public void EveryProblem_IsReportedTogether()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate(
                "  ",
                new string('t', 101),
                "No placeholder, and [a link](http://example.com).");

            Assert.Equal(
                [
                    ("heading", SubmissionAcknowledgementKeywords.TextRequired),
                    ("title", SubmissionAcknowledgementKeywords.TextTooLong),
                    ("body", SubmissionAcknowledgementKeywords.ReferenceMissing),
                    ("body", SubmissionAcknowledgementKeywords.LinkInvalid),
                ],
                errors.Select(error => (error.Field, error.Keyword)));
        }

        [Fact]
        public void Normalise_MakesEveryLineBreakALineFeed_AndTrims()
        {
            Assert.Equal("a\nb\nc", SubmissionAcknowledgementValidator.Normalise(" a\r\nb\rc \n"));
            Assert.Equal(string.Empty, SubmissionAcknowledgementValidator.Normalise(null));
        }

        private static IReadOnlyList<ValidationErrorModel> Validate(string? heading, string? title, string? body) =>
            SubmissionAcknowledgementValidator.Validate(new SaveSubmissionAcknowledgementRequestModel
            {
                Heading = heading,
                Title = title,
                Body = body,
            });

        private static IReadOnlyList<ValidationErrorModel> Validate(SaveSubmissionAcknowledgementRequestModel request) =>
            SubmissionAcknowledgementValidator.Validate(request);

        // A valid request with one text replaced.
        private static SaveSubmissionAcknowledgementRequestModel With(string field, string? value) => new()
        {
            Heading = field == "heading" ? value : "Bus Pass Request is complete",
            Title = field == "title" ? value : "Request submitted",
            Body = field == "body" ? value : BuiltInBody,
        };

        // A message keeps the placeholder, so only its length can fail.
        private static string TextOfLength(string field, int length) =>
            field == "body"
                ? Placeholder + new string('x', length - Placeholder.Length)
                : new string('x', length);
    }
}
