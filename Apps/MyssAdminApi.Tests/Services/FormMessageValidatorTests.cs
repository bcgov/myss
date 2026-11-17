namespace Myss.AdminApi.Tests.Services
{
    using System.Globalization;
    using Myss.AdminApi.Domain;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Services;
    using Myss.Common.Content;
    using Myss.FormSpec;

    /// <summary>
    /// Tests for <see cref="FormMessageValidator"/>. MyssWebCommon's
    /// <c>formMessages.unit.test.ts</c> checks the same cases against the same wording.
    /// </summary>
    public class FormMessageValidatorTests
    {
        private const string Placeholder = "{referenceNumber}";

        private static readonly FormMessageDefinition BusPass = FormMessageDefinitions.Find("bc-bus-pass", "confirmation")!;

        private static readonly FormMessageDefinition ThreePlaceholders = new(
            "test-form",
            "submitted",
            "Submitted message",
            "Shown in tests only.",
            [
                new("caseNumber", "the case number", "C-123", Required: true),
                new("submittedAt", "the time it was submitted", "14 Jul 2026 at 10:00 a.m.", Required: false),
                new("office", "the office that handles it", "Victoria", Required: false),
            ]);

        private static readonly FormMessageDefinition TwoRequired = new(
            "test-form",
            "returned",
            "Returned message",
            "Shown in tests only.",
            [
                new("caseNumber", "the case number", "C-123", Required: true),
                new("office", "the office that handles it", "Victoria", Required: true),
            ]);

        private static readonly FormMessageDefinition NoPlaceholders = new("test-form", "intro", "Introduction", "Shown in tests only.", []);

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
                Assert.Equal(FormMessageKeywords.TextRequired, error.Keyword);
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
            Assert.Equal(FormMessageKeywords.TextTooLong, error.Keyword);
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
            Assert.Equal(FormMessageKeywords.PlaceholderMissing, error.Keyword);
            Assert.Equal("Add {referenceNumber} where the applicant's reference number should appear.", error.Message);
        }

        [Theory]
        [InlineData("[track it](https://www2.gov.bc.ca/?r={referenceNumber})")]
        [InlineData("[{referenceNumber}](https://www2.gov.bc.ca)")]
        public void APlaceholderInsideALink_DoesNotCount(string link)
        {
            ValidationErrorModel error = Assert.Single(Validate(With("body", $"See {link}.")));

            Assert.Equal(FormMessageKeywords.PlaceholderMissing, error.Keyword);
        }

        [Fact]
        public void AnOverLengthMessage_IsNotCheckedFurther()
        {
            ValidationErrorModel error = Assert.Single(Validate(With("body", new string('[', 1001))));

            Assert.Equal(FormMessageKeywords.TextTooLong, error.Keyword);
        }

        [Fact]
        public void AMisspeltPlaceholder_IsUnknown_AndThePlaceholderIsMissing()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate(With("body", "Your number is {reference}. Again: {reference}."));

            Assert.Equal(
                [FormMessageKeywords.PlaceholderMissing, FormMessageKeywords.PlaceholderUnknown],
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
            Assert.Equal(FormMessageKeywords.LinkInvalid, error.Keyword);
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
                    ("heading", FormMessageKeywords.TextRequired),
                    ("title", FormMessageKeywords.TextTooLong),
                    ("body", FormMessageKeywords.PlaceholderMissing),
                    ("body", FormMessageKeywords.LinkInvalid),
                ],
                errors.Select(error => (error.Field, error.Keyword)));
        }

        [Fact]
        public void Normalise_MakesEveryLineBreakALineFeed_AndTrims()
        {
            Assert.Equal("a\nb\nc", FormMessageValidator.Normalise(" a\r\nb\rc \n"));
            Assert.Equal(string.Empty, FormMessageValidator.Normalise(null));
        }

        [Fact]
        public void AnOptionalPlaceholder_MayBeLeftOut()
        {
            Assert.Empty(Validate(ThreePlaceholders, With("body", "Your case is {caseNumber}.")));
        }

        [Fact]
        public void AMissingRequiredPlaceholder_IsNamedWithItsDescription()
        {
            ValidationErrorModel error = Assert.Single(Validate(ThreePlaceholders, With("body", "Sent {submittedAt} to {office}.")));

            Assert.Equal(FormMessageKeywords.PlaceholderMissing, error.Keyword);
            Assert.Equal("Add {caseNumber} where the case number should appear.", error.Message);
        }

        [Fact]
        public void EveryMissingRequiredPlaceholder_IsReported_InTheDefinitionsOrder()
        {
            Assert.Equal(
                [
                    "Add {caseNumber} where the case number should appear.",
                    "Add {office} where the office that handles it should appear.",
                ],
                Validate(TwoRequired, With("body", "Thank you.")).Select(error => error.Message));
        }

        [Fact]
        public void AnUnknownPlaceholder_ListsTheOnesTheMessageCanUse()
        {
            ValidationErrorModel error = Assert.Single(Validate(ThreePlaceholders, With("body", "Your case is {caseNumber}, sent {when}.")));

            Assert.Equal(FormMessageKeywords.PlaceholderUnknown, error.Keyword);
            Assert.Equal(
                "{when} is not a placeholder this message can use. The ones it can use are {caseNumber}, {submittedAt} and {office}.",
                error.Message);
        }

        [Fact]
        public void AnUnknownPlaceholder_InAMessageWithTwo_NamesBoth()
        {
            ValidationErrorModel error = Assert.Single(Validate(TwoRequired, With("body", "{caseNumber} at {office}, {when}.")));

            Assert.Equal(
                "{when} is not a placeholder this message can use. The ones it can use are {caseNumber} and {office}.",
                error.Message);
        }

        [Fact]
        public void UnknownPlaceholders_AreReportedOnce_InTheOrderTheyFirstAppear()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate(With("body", Placeholder + " {b} then {a} then {b}."));

            Assert.Equal(
                [
                    "{b} is not a placeholder this message can use. The only one is {referenceNumber}.",
                    "{a} is not a placeholder this message can use. The only one is {referenceNumber}.",
                ],
                errors.Select(error => error.Message));
        }

        [Fact]
        public void AnUnknownPlaceholderInsideALink_IsRefused()
        {
            ValidationErrorModel error = Assert.Single(
                Validate(With("body", Placeholder + ". [Track it](https://www2.gov.bc.ca/?r={track}).")));

            Assert.Equal(FormMessageKeywords.PlaceholderUnknown, error.Keyword);
            Assert.Equal(
                "{track} is not a placeholder this message can use. The only one is {referenceNumber}.",
                error.Message);
        }

        [Fact]
        public void TheKeywords_AreTheOnesTheAdminSiteMatches()
        {
            Assert.Equal(
                [
                    "FORM_MESSAGE.TEXT.REQUIRED",
                    "FORM_MESSAGE.TEXT.TOO_LONG",
                    "FORM_MESSAGE.BODY.PLACEHOLDER_MISSING",
                    "FORM_MESSAGE.BODY.PLACEHOLDER_UNKNOWN",
                    "FORM_MESSAGE.BODY.LINK_INVALID",
                    "FORM_MESSAGE.CHANGED",
                ],
                [
                    FormMessageKeywords.TextRequired,
                    FormMessageKeywords.TextTooLong,
                    FormMessageKeywords.PlaceholderMissing,
                    FormMessageKeywords.PlaceholderUnknown,
                    FormMessageKeywords.LinkInvalid,
                    FormMessageKeywords.Changed,
                ]);
        }

        [Fact]
        public void AMessageWithNoPlaceholders_RefusesAnyPlaceholder_AndNeedsNone()
        {
            Assert.Empty(Validate(NoPlaceholders, With("body", "Welcome.")));

            ValidationErrorModel error = Assert.Single(Validate(NoPlaceholders, With("body", "Welcome, {name}.")));
            Assert.Equal(FormMessageKeywords.PlaceholderUnknown, error.Keyword);
            Assert.Equal("{name} is not a placeholder this message can use.", error.Message);
        }

        private static IReadOnlyList<ValidationErrorModel> Validate(string? heading, string? title, string? body) =>
            FormMessageValidator.Validate(BusPass, new SaveFormMessageRequestModel
            {
                Heading = heading,
                Title = title,
                Body = body,
            });

        private static IReadOnlyList<ValidationErrorModel> Validate(SaveFormMessageRequestModel request) =>
            FormMessageValidator.Validate(BusPass, request);

        private static IReadOnlyList<ValidationErrorModel> Validate(
            FormMessageDefinition definition,
            SaveFormMessageRequestModel request) =>
            FormMessageValidator.Validate(definition, request);

        // A valid request with one text replaced.
        private static SaveFormMessageRequestModel With(string field, string? value) => new()
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
