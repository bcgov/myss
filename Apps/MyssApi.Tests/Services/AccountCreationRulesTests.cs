namespace Myss.Api.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Myss.Data;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Tests for <see cref="AccountCreationRules"/>: who the account is for and
    /// that context's own answers, at each boundary. The web client's copy of
    /// the rules is held to the same cases.
    /// </summary>
    public class AccountCreationRulesTests
    {
        [Fact]
        public void NoAnswers_AskForTheContext()
        {
            ValidationErrorModel error = Assert.Single(AccountCreationRules.Validate(null, signedInWithBceid: true));

            Assert.Equal(AccountCreationRules.ContextField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationContextRequired, error.Keyword);
            Assert.Equal("Select the option that describes you.", error.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankContext_IsRequired(string context)
        {
            ValidationErrorModel error = Assert.Single(Validate(new AccountCreationRequestModel { Context = context }));

            Assert.Equal(ValidationKeywords.RegistrationContextRequired, error.Keyword);
        }

        [Theory]
        [InlineData("helper")]
        [InlineData("Self")]
        [InlineData("with-helper")]
        public void UnknownContext_IsRefused(string context)
        {
            ValidationErrorModel error = Assert.Single(Validate(new AccountCreationRequestModel { Context = context }));

            Assert.Equal(AccountCreationRules.ContextField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationContextUnknown, error.Keyword);
            Assert.Equal("Select one of the options shown.", error.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Self_NeedsNothingMore_FromEitherSignIn(bool signedInWithBceid)
        {
            Assert.Empty(AccountCreationRules.Validate(new AccountCreationRequestModel { Context = "self" }, signedInWithBceid));
        }

        [Fact]
        public void AnswersOutsideTheContext_AreIgnored()
        {
            var model = new AccountCreationRequestModel
            {
                Context = "self",
                HelperAuthorized = true,
                Representative = new RepresentativeRequestModel { Phone = "123" },
                ConsentDocumentIds = Ids(6),
                PowerOfAttorneyDocumentIds = Ids(6),
            };

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData("legalRepresentative")]
        [InlineData("parentForMinor")]
        public void RepresentativeContext_WithBceid_Passes(string context)
        {
            Assert.Empty(AccountCreationRules.Validate(Representative(context), signedInWithBceid: true));
        }

        [Theory]
        [InlineData("legalRepresentative")]
        [InlineData("parentForMinor")]
        public void RepresentativeContext_WithoutBceid_IsRefused(string context)
        {
            ValidationErrorModel error = Assert.Single(AccountCreationRules.Validate(Representative(context), signedInWithBceid: false));

            Assert.Equal(AccountCreationRules.ContextField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationContextSignInNotAllowed, error.Keyword);
            Assert.Equal("To create an account for someone else, sign in with Basic BCeID.", error.Message);
        }

        [Fact]
        public void WithHelper_WithoutAnAuthorization_IsRefused()
        {
            ValidationErrorModel error = Assert.Single(Validate(new AccountCreationRequestModel { Context = "withHelper" }));

            Assert.Equal(AccountCreationRules.HelperAuthorizedField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationHelperAuthorizationRequired, error.Keyword);
            Assert.Equal("Select whether we may share your information with this person.", error.Message);
        }

        [Fact]
        public void WithHelper_No_NeedsNoDocument_AndIgnoresOnesSent()
        {
            var model = new AccountCreationRequestModel { Context = "withHelper", HelperAuthorized = false, ConsentDocumentIds = Ids(6) };

            Assert.Empty(AccountCreationRules.Validate(model, signedInWithBceid: false));
        }

        [Fact]
        public void WithHelper_Yes_WithoutAConsentForm_IsRefused()
        {
            var model = new AccountCreationRequestModel { Context = "withHelper", HelperAuthorized = true, ConsentDocumentIds = [] };

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(AccountCreationRules.ConsentDocumentsField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationDocumentConsentRequired, error.Keyword);
            Assert.Equal("Attach your signed Consent to Disclosure of Information form.", error.Message);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public void WithHelper_Yes_WithUpToFiveConsentForms_Passes(int count)
        {
            var model = new AccountCreationRequestModel { Context = "withHelper", HelperAuthorized = true, ConsentDocumentIds = Ids(count) };

            Assert.Empty(AccountCreationRules.Validate(model, signedInWithBceid: false));
        }

        [Fact]
        public void WithHelper_Yes_WithSixConsentForms_IsRefused()
        {
            var model = new AccountCreationRequestModel { Context = "withHelper", HelperAuthorized = true, ConsentDocumentIds = Ids(6) };

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(AccountCreationRules.ConsentDocumentsField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationDocumentTooMany, error.Keyword);
            Assert.Equal("Attach no more than 5 documents.", error.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankFirstName_IsRequired(string? firstName)
        {
            var model = Representative("legalRepresentative");
            model.Representative!.FirstName = firstName;

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(AccountCreationRules.RepresentativeFirstNameField, error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
            Assert.Equal("Enter your first name.", error.Message);
        }

        [Fact]
        public void BlankLastName_IsRequired()
        {
            var model = Representative("parentForMinor");
            model.Representative!.LastName = " ";

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(AccountCreationRules.RepresentativeLastNameField, error.Field);
            Assert.Equal("Enter your last name.", error.Message);
        }

        [Fact]
        public void NamesOf200Characters_Pass_AfterTrimming()
        {
            var model = Representative("legalRepresentative");
            model.Representative!.FirstName = "  " + new string('a', 200) + "  ";
            model.Representative.LastName = new string('b', 200);

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData(AccountCreationRules.RepresentativeFirstNameField)]
        [InlineData(AccountCreationRules.RepresentativeLastNameField)]
        public void NameOf201Characters_IsTooLong(string field)
        {
            var model = Representative("parentForMinor");
            if (field == AccountCreationRules.RepresentativeFirstNameField)
            {
                model.Representative!.FirstName = new string('a', 201);
            }
            else
            {
                model.Representative!.LastName = new string('a', 201);
            }

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(field, error.Field);
            Assert.Equal(ValidationKeywords.FieldMaxLength, error.Keyword);
            Assert.Equal("This answer is too long.", error.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankAgency_IsAllowed(string? agency)
        {
            var model = Representative("legalRepresentative");
            model.Representative!.Agency = agency;

            Assert.Empty(Validate(model));
        }

        [Fact]
        public void AgencyOf200Characters_Passes()
        {
            var model = Representative("legalRepresentative");
            model.Representative!.Agency = new string('a', 200);

            Assert.Empty(Validate(model));
        }

        [Fact]
        public void AgencyOf201Characters_IsTooLong_ForALegalRepresentative()
        {
            var model = Representative("legalRepresentative");
            model.Representative!.Agency = new string('a', 201);

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(AccountCreationRules.RepresentativeAgencyField, error.Field);
            Assert.Equal(ValidationKeywords.FieldMaxLength, error.Keyword);
        }

        [Fact]
        public void Agency_IsIgnored_ForAParent()
        {
            var model = Representative("parentForMinor");
            model.Representative!.Agency = new string('a', 201);

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData("(250) 555-0123")]
        [InlineData("2505550123")]
        [InlineData("250 555 0123")]
        [InlineData("250-555-0123")]
        public void ValidPhone_Passes(string phone)
        {
            var model = Representative("parentForMinor");
            model.Representative!.Phone = phone;

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("(150) 555-0123")]
        [InlineData("(250) 555-012a")]
        [InlineData("(250) 555-01234")]
        public void InvalidPhone_IsRefused(string phone)
        {
            var model = Representative("legalRepresentative");
            model.Representative!.Phone = phone;

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(AccountCreationRules.RepresentativePhoneField, error.Field);
            Assert.Equal(ValidationKeywords.PhoneInvalidFormat, error.Keyword);
            Assert.Equal("Phone number is invalid", error.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void MissingPhone_IsRequired(string? phone)
        {
            var model = Representative("legalRepresentative");
            model.Representative!.Phone = phone;

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(AccountCreationRules.RepresentativePhoneField, error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
            Assert.Equal("Enter your phone number.", error.Message);
        }

        [Fact]
        public void FivePowerOfAttorneyDocuments_Pass()
        {
            var model = Representative("legalRepresentative");
            model.PowerOfAttorneyDocumentIds = Ids(5);

            Assert.Empty(Validate(model));
        }

        [Fact]
        public void SixPowerOfAttorneyDocuments_AreTooMany()
        {
            var model = Representative("legalRepresentative");
            model.PowerOfAttorneyDocumentIds = Ids(6);

            ValidationErrorModel error = Assert.Single(Validate(model));

            Assert.Equal(AccountCreationRules.PowerOfAttorneyDocumentsField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationDocumentTooMany, error.Keyword);
        }

        [Fact]
        public void PowerOfAttorneyDocuments_AreIgnored_ForAParent()
        {
            var model = Representative("parentForMinor");
            model.PowerOfAttorneyDocumentIds = Ids(6);

            Assert.Empty(Validate(model));
        }

        [Fact]
        public void SeveralFailures_AreReportedTogether()
        {
            var model = new AccountCreationRequestModel { Context = "legalRepresentative" };

            IReadOnlyList<ValidationErrorModel> errors = AccountCreationRules.Validate(model, signedInWithBceid: false);

            Assert.Equal(
                [
                    AccountCreationRules.ContextField,
                    AccountCreationRules.RepresentativeFirstNameField,
                    AccountCreationRules.RepresentativeLastNameField,
                    AccountCreationRules.RepresentativePhoneField,
                ],
                errors.Select(e => e.Field));
        }

        [Fact]
        public void NoError_IsFlaggedAsAuthored()
        {
            var model = new AccountCreationRequestModel
            {
                Context = "legalRepresentative",
                Representative = new RepresentativeRequestModel { Phone = "123", Agency = new string('a', 201) },
                PowerOfAttorneyDocumentIds = Ids(6),
            };

            IReadOnlyList<ValidationErrorModel> errors = AccountCreationRules.Validate(model, signedInWithBceid: false);

            Assert.Equal(6, errors.Count);
            Assert.All(errors, e => Assert.False(e.HasAuthoredMessage));
        }

        [Theory]
        [InlineData("self", AccountCreationContext.Self)]
        [InlineData("withHelper", AccountCreationContext.WithHelper)]
        [InlineData("legalRepresentative", AccountCreationContext.LegalRepresentative)]
        [InlineData("parentForMinor", AccountCreationContext.ParentForMinor)]
        public void TryParseContext_ReadsEachOfTheFour(string value, AccountCreationContext expected)
        {
            Assert.True(AccountCreationRules.TryParseContext(value, out AccountCreationContext context));
            Assert.Equal(expected, context);
        }

        private static IReadOnlyList<ValidationErrorModel> Validate(AccountCreationRequestModel model) =>
            AccountCreationRules.Validate(model, signedInWithBceid: true);

        private static AccountCreationRequestModel Representative(string context) => new()
        {
            Context = context,
            Representative = new RepresentativeRequestModel
            {
                FirstName = "Pat",
                LastName = "Lee",
                Phone = "(250) 555-0123",
                Agency = context == "legalRepresentative" ? "Northern Trustee Services" : null,
            },
        };

        private static List<Guid> Ids(int count) =>
            Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
    }
}
