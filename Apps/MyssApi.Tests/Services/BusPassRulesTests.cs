namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="BusPassRules"/>: the legacy form's second
    /// validation pass, now run server-side.
    /// </summary>
    public class BusPassRulesTests
    {
        private static readonly DateOnly Today = new(2026, 9, 10);

        [Fact]
        public void CompleteNewApplicantAnswers_Pass()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate(NewApplicant());

            Assert.Empty(errors);
        }

        [Fact]
        public void CompleteExistingClientAnswers_Pass()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate(ExistingClient());

            Assert.Empty(errors);
        }

        [Fact]
        public void NeitherSinNorAccountNumber_IsRefused()
        {
            var answers = NewApplicant();
            answers.Remove("socialInsuranceNumber");

            ValidationErrorModel error = Assert.Single(Validate(answers));
            Assert.Equal("socialInsuranceNumber", error.Field);
            Assert.Equal(BusPassErrorKeywords.IdentifierRequired, error.Keyword);
        }

        [Fact]
        public void AccountNumberAlone_SatisfiesTheIdentifierRule()
        {
            var answers = NewApplicant();
            answers.Remove("socialInsuranceNumber");
            answers["busPassAccountNumber"] = "123456789";

            Assert.Empty(Validate(answers));
        }

        [Fact]
        public void MaskedSinOfUnderscoresOnly_CountsAsMissing()
        {
            // The legacy form's input mask left "___ ___ ___" behind for an
            // empty field; digits are what count.
            var answers = NewApplicant();
            answers["socialInsuranceNumber"] = "___ ___ ___";

            Assert.Contains(Validate(answers), e => e.Keyword == BusPassErrorKeywords.IdentifierRequired);
        }

        [Fact]
        public void ImpossibleDate_IsRefusedOnTheDayField()
        {
            var answers = NewApplicant();
            answers["birthDay"] = "31";
            answers["birthMonth"] = "02";

            ValidationErrorModel error = Assert.Single(Validate(answers));
            Assert.Equal("birthDay", error.Field);
            Assert.Equal(BusPassErrorKeywords.DateOfBirthInvalid, error.Keyword);
        }

        [Fact]
        public void DateOfBirthAfterToday_IsRefused()
        {
            var answers = NewApplicant();
            answers["birthYear"] = "2027";

            ValidationErrorModel error = Assert.Single(Validate(answers));
            Assert.Equal(BusPassErrorKeywords.DateOfBirthInFuture, error.Keyword);
        }

        [Theory]
        [InlineData("2010", "09", "11", true)]
        [InlineData("2010", "09", "10", false)]
        [InlineData("2010", "09", "09", false)]
        public void SixteenthBirthday_IsTheBoundary(string year, string month, string day, bool refused)
        {
            var answers = NewApplicant();
            answers["birthYear"] = year;
            answers["birthMonth"] = month;
            answers["birthDay"] = day;

            IReadOnlyList<ValidationErrorModel> errors = Validate(answers);

            Assert.Equal(refused, errors.Any(e => e.Keyword == BusPassErrorKeywords.Under16));
        }

        [Fact]
        public void PreferringEmailWithoutAnAddress_IsRefused()
        {
            var answers = NewApplicant();
            answers["preferredCommunication"] = "email";
            answers.Remove("email");

            ValidationErrorModel error = Assert.Single(Validate(answers));
            Assert.Equal("email", error.Field);
            Assert.Equal(BusPassErrorKeywords.EmailRequiredForEmailContact, error.Keyword);
        }

        [Fact]
        public void PreferringPhoneWithoutAnEmail_Passes()
        {
            var answers = NewApplicant();
            answers.Remove("email");

            Assert.Empty(Validate(answers));
        }

        [Fact]
        public void EmailVerificationNotMatchingEmail_IsRefused()
        {
            var answers = NewApplicant();
            answers["email"] = "ada@example.com";
            answers["emailVerification"] = "ada@exampel.com";

            ValidationErrorModel error = Assert.Single(Validate(answers));
            Assert.Equal("emailVerification", error.Field);
            Assert.Equal(BusPassErrorKeywords.EmailMismatch, error.Keyword);
        }

        [Fact]
        public void EmailVerificationMatchingEmail_Passes()
        {
            var answers = NewApplicant();
            answers["email"] = "ada@example.com";
            answers["emailVerification"] = "Ada@Example.com";

            Assert.Empty(Validate(answers));
        }

        [Fact]
        public void ExistingClientWithoutAReason_IsRefused()
        {
            // Conditionally required in the spec, which the spec validator
            // exempts; the rule closes that gap.
            var answers = ExistingClient();
            answers.Remove("existingClientReason");

            ValidationErrorModel error = Assert.Single(Validate(answers));
            Assert.Equal("existingClientReason", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void NewApplicantWithoutCategoryOrAcknowledgement_ReportsBoth()
        {
            var answers = NewApplicant();
            answers.Remove("eligibilityCategory");
            answers["eligibilityAcknowledged"] = false;

            IReadOnlyList<ValidationErrorModel> errors = Validate(answers);

            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.Field == "eligibilityCategory");
            Assert.Contains(errors, e => e.Field == "eligibilityAcknowledged");
        }

        [Fact]
        public void InvalidV3RequestType_IsRefused()
        {
            var answers = V3NewApplication();
            answers["serviceRequestType"] = "unknown";

            ValidationErrorModel error = Assert.Single(Validate(answers));

            Assert.Equal("serviceRequestType", error.Field);
            Assert.Equal(BusPassErrorKeywords.RequestTypeInvalid, error.Keyword);
        }

        [Fact]
        public void V3NewApplicationWithoutEligibilityCategory_IsRefused()
        {
            var answers = V3NewApplication();
            answers.Remove("eligibilityCategory");

            ValidationErrorModel error = Assert.Single(Validate(answers));

            Assert.Equal("eligibilityCategory", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void V3NewApplicationWithoutAcknowledgement_IsRefused()
        {
            var answers = V3NewApplication();
            answers.Remove("eligibilityAcknowledged");

            ValidationErrorModel error = Assert.Single(Validate(answers));

            Assert.Equal("eligibilityAcknowledged", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void V4ReplacementWithoutAcknowledgement_IsRefused()
        {
            ValidationErrorModel error = Assert.Single(
                Validate(ReplacementAnswers(), requireReplacementAcknowledgement: true));

            Assert.Equal(BusPassAnswers.AcknowledgedPassCancellation, error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void V4ReplacementWithFalseAcknowledgement_IsRefused()
        {
            var answers = ReplacementAnswers();
            answers[BusPassAnswers.AcknowledgedPassCancellation] = false;

            ValidationErrorModel error = Assert.Single(
                Validate(answers, requireReplacementAcknowledgement: true));

            Assert.Equal(BusPassAnswers.AcknowledgedPassCancellation, error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void V4ReplacementWithAcknowledgement_Passes()
        {
            var answers = ReplacementAnswers();
            answers[BusPassAnswers.AcknowledgedPassCancellation] = true;

            Assert.Empty(Validate(answers, requireReplacementAcknowledgement: true));
        }

        [Fact]
        public void V3ReplacementWithoutAcknowledgement_RemainsCompatible()
        {
            Assert.Empty(Validate(ReplacementAnswers()));
        }

        [Fact]
        public void DifferentMailingAddressWithMissingParts_ReportsEachPart()
        {
            var answers = NewApplicant();
            answers["mailingAddressDifferent"] = "yes";
            answers["mailingStreetAddress1"] = "PO Box 1";

            IReadOnlyList<ValidationErrorModel> errors = Validate(answers);

            Assert.Equal(
                ["mailingCity", "mailingProvince", "mailingPostalCode"],
                errors.Select(e => e.Field).ToArray());
            Assert.All(errors, e => Assert.Equal(ValidationKeywords.FieldRequired, e.Keyword));
        }

        [Fact]
        public void EveryFailureIsReportedAtOnce()
        {
            var answers = new Dictionary<string, object?>
            {
                ["applicantCategory"] = "new",
                ["preferredCommunication"] = "email",
            };

            IReadOnlyList<ValidationErrorModel> errors = Validate(answers);

            Assert.True(errors.Count >= 5, $"expected at least 5 errors, got {errors.Count}");
        }

        private static IReadOnlyList<ValidationErrorModel> Validate(
            Dictionary<string, object?> answers,
            bool requireReplacementAcknowledgement = false)
        {
            using JsonDocument doc = JsonSerializer.SerializeToDocument(answers);
            return BusPassRules.Validate(doc.RootElement.Clone(), Today, requireReplacementAcknowledgement);
        }

        private static Dictionary<string, object?> NewApplicant() => new()
        {
            ["applicantCategory"] = "new",
            ["eligibilityAcknowledged"] = true,
            ["eligibilityCategory"] = "over65",
            ["socialInsuranceNumber"] = "046 454 286",
            ["firstName"] = "Ada",
            ["lastName"] = "Lovelace",
            ["birthDay"] = "10",
            ["birthMonth"] = "12",
            ["birthYear"] = "1950",
            ["phoneNumber"] = "(250) 555-0199",
            ["phoneType"] = "home",
            ["email"] = "ada@example.com",
            ["emailVerification"] = "ada@example.com",
            ["preferredCommunication"] = "phone",
            ["streetAddress1"] = "501 Belleville St",
            ["city"] = "Victoria",
            ["province"] = "BC",
            ["postalCode"] = "V8V 1X4",
            ["mailingAddressDifferent"] = "no",
        };

        private static Dictionary<string, object?> V3NewApplication()
        {
            var answers = NewApplicant();
            answers.Remove("applicantCategory");
            answers["serviceRequestType"] = "newApplication";
            return answers;
        }

        // The v3/v4 answer shape is identical; only the acknowledgement
        // requirement differs, driven by the version flag passed to Validate.
        private static Dictionary<string, object?> ReplacementAnswers()
        {
            var answers = ExistingClient();
            answers.Remove("applicantCategory");
            answers.Remove("existingClientReason");
            answers["serviceRequestType"] = "replacement";
            return answers;
        }

        private static Dictionary<string, object?> ExistingClient()
        {
            var answers = NewApplicant();
            answers["applicantCategory"] = "existing";
            answers["existingClientReason"] = "moved";
            answers.Remove("eligibilityAcknowledged");
            answers.Remove("eligibilityCategory");
            return answers;
        }
    }
}
