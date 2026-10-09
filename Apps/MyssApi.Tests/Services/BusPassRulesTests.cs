namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="BusPassRules"/>: the cross-field rules the spec
    /// cannot express, run server-side. Everything a single field declares,
    /// the conditionally required fields included, is
    /// <see cref="FormSpecValidator"/>'s job and is pinned there.
    /// </summary>
    public class BusPassRulesTests
    {
        private static readonly DateOnly Today = new(2026, 9, 10);

        [Fact]
        public void CompleteNewApplicantAnswers_Pass()
        {
            Assert.Empty(Validate(NewApplicant()));
        }

        [Fact]
        public void CompleteExistingClientAnswers_Pass()
        {
            Assert.Empty(Validate(ExistingClient()));
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
        public void EmailVerificationNotMatchingEmail_IsRefused_ForFormsBeforeV6()
        {
            // Up to v5 the spec checked the match only in browser script, so
            // this is the server-side check for those versions.
            var answers = NewApplicant();
            answers["email"] = "ada@example.com";
            answers["emailVerification"] = "ada@exampel.com";

            ValidationErrorModel error = Assert.Single(Validate(answers, formSpecVersion: 5));
            Assert.Equal("emailVerification", error.Field);
            Assert.Equal(BusPassErrorKeywords.EmailMismatch, error.Keyword);
        }

        [Fact]
        public void EmailVerificationMismatch_IsTheSpecsJob_FromV6()
        {
            // v6 names the partner field (properties.myssMatches), so the spec
            // validator reports the mismatch and this class must not do it twice.
            var answers = NewApplicant();
            answers["email"] = "ada@example.com";
            answers["emailVerification"] = "ada@exampel.com";

            Assert.Empty(Validate(answers, formSpecVersion: BusPassRules.SpecValidatesEmailMatchVersion));
        }

        [Fact]
        public void EmailVerificationMatchingEmail_Passes()
        {
            var answers = NewApplicant();
            answers["email"] = "ada@example.com";
            answers["emailVerification"] = "Ada@Example.com";

            Assert.Empty(Validate(answers, formSpecVersion: 5));
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
        public void MissingRequestType_IsTheSpecsJob()
        {
            // The selector is required by the spec; only a value the form does
            // not offer is this class's business.
            var answers = NewApplicant();
            answers.Remove("applicantCategory");

            Assert.DoesNotContain(Validate(answers), e => e.Field == "serviceRequestType" || e.Field == "applicantCategory");
        }

        [Fact]
        public void LegacyCategoryTheFormDoesNotOffer_IsRefused()
        {
            // v1/v2 shape. Unrefused, the mapper would default it to a new
            // application.
            var answers = NewApplicant();
            answers["applicantCategory"] = "bogus";

            ValidationErrorModel error = Assert.Single(Validate(answers));
            Assert.Equal("applicantCategory", error.Field);
            Assert.Equal(BusPassErrorKeywords.RequestTypeInvalid, error.Keyword);
        }

        [Fact]
        public void LegacyReasonTheFormDoesNotOffer_IsRefused()
        {
            var answers = ExistingClient();
            answers["existingClientReason"] = "bogus";

            ValidationErrorModel error = Assert.Single(Validate(answers));
            Assert.Equal("existingClientReason", error.Field);
            Assert.Equal(BusPassErrorKeywords.RequestTypeInvalid, error.Keyword);
        }

        [Fact]
        public void LegacyExistingClientWithoutAReason_IsTheSpecsJob()
        {
            // The reason is required by the spec behind a conditional, which
            // the spec validator reports; this class must not say it twice.
            var answers = ExistingClient();
            answers.Remove("existingClientReason");

            Assert.Empty(Validate(answers));
        }

        [Fact]
        public void ConditionallyRequiredFields_AreTheSpecsJob()
        {
            // Each of these is required by the spec behind a conditional, which
            // the spec validator now evaluates; reporting them here again would
            // list every one of them twice.
            var existing = ExistingClient();
            existing.Remove("existingClientReason");
            Assert.Empty(Validate(existing));

            var newApplicant = NewApplicant();
            newApplicant.Remove("eligibilityCategory");
            newApplicant["eligibilityAcknowledged"] = false;
            Assert.Empty(Validate(newApplicant));

            var mailing = NewApplicant();
            mailing["mailingAddressDifferent"] = "yes";
            mailing["mailingStreetAddress1"] = "PO Box 1";
            Assert.Empty(Validate(mailing));

            var replacement = ExistingClient();
            replacement.Remove("applicantCategory");
            replacement.Remove("existingClientReason");
            replacement["serviceRequestType"] = "replacement";
            Assert.Empty(Validate(replacement, formSpecVersion: 4));
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

            Assert.Equal(
                [BusPassErrorKeywords.IdentifierRequired, BusPassErrorKeywords.DateOfBirthInvalid, BusPassErrorKeywords.EmailRequiredForEmailContact],
                errors.Select(e => e.Keyword).ToArray());
        }

        private static IReadOnlyList<ValidationErrorModel> Validate(
            Dictionary<string, object?> answers,
            int formSpecVersion = 0)
        {
            using JsonDocument doc = JsonSerializer.SerializeToDocument(answers);
            return BusPassRules.Validate(doc.RootElement.Clone(), Today, formSpecVersion);
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
