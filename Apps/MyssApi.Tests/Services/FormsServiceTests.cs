namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.FeatureManagement;
    using Myss.Api.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;
    using Myss.Data;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;
    using Myss.Common.Auth;

    /// <summary>
    /// Tests for <see cref="FormsService"/>.
    /// </summary>
    public class FormsServiceTests
    {
        private const string SpecWithSin = """
            {
              "components": [
                { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
                { "type": "textfield", "key": "sin", "input": true, "properties": { "myssValidator": "sin" } }
              ]
            }
            """;

        private readonly FakeFormSpecProvider _provider = new();
        private readonly FakeErrorMessageProvider _errorMessages = new();
        private readonly PinHasher _pinHasher = new();
        private readonly FakeEmailProvider _email = new();
        private readonly FakeContactMatchProvider _contactMatch = new();
        // Off by default: these tests are about the form, not Step 2's email proof (MYSS-295),
        // which the tests that need it switch on.
        private IFeatureManager _features = TestFeatures.AllOnExcept(FeatureFlags.EmailVerification);

        [Fact]
        public async Task Submit_RefusedAnswers_AreWordedFromTheCatalogue_KeywordByKeyword()
        {
            // The validator emits the compiled default; the wording the Service
            // Designer published replaces it. The required failure has no
            // catalogue row and keeps the validator's text.
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 1, SpecWithSin);
            _errorMessages.Overrides[ValidationKeywords.SinInvalidChecksum] = "Authored SIN wording";
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "poc-test-form", Request(1, """{"sin":"050082830"}"""), CancellationToken.None);

            Assert.False(result.IsValid);
            ValidationErrorModel sin = Assert.Single(result.Errors, e => e.Field == "sin");
            Assert.Equal(ValidationKeywords.SinInvalidChecksum, sin.Keyword);
            Assert.Equal("Authored SIN wording", sin.Message);
            ValidationErrorModel required = Assert.Single(result.Errors, e => e.Field == "firstName");
            Assert.Equal(ValidationKeywords.FieldRequired, required.Keyword);
            Assert.Equal("This answer is required.", required.Message);
            Assert.Equal(1, _errorMessages.Calls);
        }

        [Fact]
        public async Task Submit_AcceptedAnswers_NeverReadTheCatalogue()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 1, SpecWithSin);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "poc-test-form", Request(1, """{"firstName":"Ada","sin":"050082833"}"""), CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(0, _errorMessages.Calls);
        }

        [Fact]
        public async Task GetErrorMessages_ServesTheCatalogue()
        {
            using FormsDbContext db = NewDb();
            _errorMessages.Overrides["NEW.KEYWORD.FROM_STRAPI"] = "Authored wording";
            FormsService service = NewService(db);

            IReadOnlyDictionary<string, string> catalogue = await service.GetErrorMessagesAsync(CancellationToken.None);

            Assert.Equal("Authored wording", catalogue["NEW.KEYWORD.FROM_STRAPI"]);
            Assert.Equal(
                ErrorMessageDefaults.Messages[ValidationKeywords.SinWrongLength],
                catalogue[ValidationKeywords.SinWrongLength]);
        }

        [Fact]
        public async Task Submit_ResolvesTheClaimedVersion_NeverTheLatest()
        {
            // §7.2, non-negotiable: a citizen part-way through a form when a
            // designer publishes a new version must be validated against the
            // rules they were actually shown.
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 2, SpecWithFirstName);
            _provider.LatestResult = FakeFormSpecProvider.Spec("poc-test-form", 3);
            FormsService service = NewService(db);

            await service.SubmitAsync("poc-test-form", Request(2, """{"firstName":"Grace"}"""), CancellationToken.None);

            Assert.Equal([("poc-test-form", 2)], _provider.VersionCalls);
            Assert.Empty(_provider.LatestCalls);
        }

        [Fact]
        public async Task Submit_StampsTheRenderedVersion_AndPersistsAnswers()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 2, SpecWithFirstName);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "poc-test-form", Request(2, """{"firstName":"Grace","monthlyIncome":2000}"""), CancellationToken.None);

            Assert.True(result.IsValid);
            FormSubmission row = Assert.Single(await db.FormSubmissions.ToListAsync());
            Assert.Equal(result.Submission!.Id, row.Id);
            Assert.Equal("poc-test-form", row.FormSpecId);
            Assert.Equal(2, row.FormSpecVersion);
            Assert.Equal("Grace", row.Answers.RootElement.GetProperty("firstName").GetString());
            Assert.True(result.Submission.SubmittedAt <= DateTimeOffset.UtcNow);
            Assert.True(result.Submission.SubmittedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task Submit_UnknownOrUnpublishedVersion_IsRefused_AndNothingIsPersisted()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = null;
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "poc-test-form", Request(99, """{"firstName":"Grace"}"""), CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Equal(ValidationKeywords.VersionUnknown, Assert.Single(result.Errors).Keyword);
            Assert.Empty(await db.FormSubmissions.ToListAsync());
        }

        [Fact]
        public async Task Submit_InvalidAnswers_AreRefused_AndNothingIsPersisted()
        {
            // The important half: a refused submission must leave no trace.
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 2, SpecWithFirstName);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "poc-test-form", Request(2, """{"unknownField":"x"}"""), CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Null(result.Submission);
            Assert.Empty(await db.FormSubmissions.ToListAsync());
        }

        /// <summary>A spec with one required field, for submit-path arrangements.</summary>
        private const string SpecWithFirstName = """
        {
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "number", "key": "monthlyIncome", "input": true }
          ]
        }
        """;

                private const string RegistrationSpec = """
                {
                    "components": [
                        { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
                        { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
                        { "type": "datetime", "key": "dateOfBirth", "input": true, "validate": { "required": true } },
                        { "type": "email", "key": "email", "input": true, "validate": { "required": true } },
                        { "type": "textfield", "key": "sin", "input": true, "properties": { "myssValidator": "sin" }, "validate": { "required": true } }
                    ]
                }
                """;

                [Fact]
                public async Task Submit_RegistrationWithMalformedDateOfBirth_IsRefused_AndNothingIsPersisted()
                {
                        using FormsDbContext db = NewDb();
                        _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
                        FormsService service = NewService(db);

                        FormSubmissionResultModel result = await service.SubmitAsync(
                                "registration",
                                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"not-a-date","email":"ada@example.com","sin":"050082833"}"""),
                                CancellationToken.None);

                        ValidationErrorModel error = Assert.Single(result.Errors);
                        Assert.Equal("dateOfBirth", error.Field);
                        Assert.Equal(ValidationKeywords.RegistrationDateOfBirthInvalid, error.Keyword);
                        Assert.Empty(await db.FormSubmissions.ToListAsync());
                        Assert.Empty(await db.MyssUserProfiles.ToListAsync());
                }

                [Fact]
                public async Task Submit_RegistrationWithFutureDateOfBirth_IsRefused_AndNothingIsPersisted()
                {
                        using FormsDbContext db = NewDb();
                        _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
                        FormsService service = NewService(db);

                        FormSubmissionResultModel result = await service.SubmitAsync(
                                "registration",
                                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"2999-01-01","email":"ada@example.com","sin":"050082833"}"""),
                                CancellationToken.None);

                        ValidationErrorModel error = Assert.Single(result.Errors);
                        Assert.Equal("dateOfBirth", error.Field);
                        Assert.Equal(ValidationKeywords.RegistrationDateOfBirthInFuture, error.Keyword);
                        Assert.Empty(await db.FormSubmissions.ToListAsync());
                        Assert.Empty(await db.MyssUserProfiles.ToListAsync());
                }

        [Fact]
        public async Task Submit_RegistrationV1_IsRefusedAsARetiredVersion_AndNothingIsPersisted()
        {
            // The seeded v1: a first name only, which can never make a profile.
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec(
                "registration",
                1,
                """{"components":[{ "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } }]}""");
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(1, """{"firstName":"Ada"}"""),
                CancellationToken.None);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal(nameof(FormSubmissionRequestModel.FormSpecVersion), error.Field);
            Assert.Equal(ValidationKeywords.VersionUnknown, error.Keyword);
            Assert.Empty(await db.FormSubmissions.ToListAsync());
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        [Fact]
        public async Task Submit_RegistrationV2_IsTheOldestVersionThatStillRegisters()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 2, RegistrationSpec);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(2, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.Empty(result.Errors);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Equal("Lovelace", profile.LastName);
            Assert.Null(profile.Phone);
        }

        [Fact]
        public async Task Submit_RegistrationWithPickerDateTime_PersistsTheDatePortion()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10T12:30:00+00:00","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Equal(new DateOnly(1815, 12, 10), profile.DateOfBirth);
        }

        [Fact]
        public async Task Submit_RegistrationWithoutAuthenticatedIdentity_ThrowsAndPersistsNothing()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            FormsService service = NewService(db, new AnonymousCurrentUserAccessor());

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SubmitAsync(
                    "registration",
                    Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                    CancellationToken.None));

            Assert.Equal("An authenticated identity is required to register a MySS profile.", exception.Message);
            Assert.Empty(await db.FormSubmissions.ToListAsync());
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        [Fact]
        public async Task Submit_RegistrationCreatesAndUpdatesTheProfileForTheAuthenticatedSubject()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            FormsService service = NewService(db);

            FormSubmissionResultModel firstResult = await service.SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);
            FormSubmissionResultModel secondResult = await service.SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Augusta","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"augusta@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(firstResult.IsValid);
            Assert.True(secondResult.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Equal("test-subject", profile.Subject);
            Assert.Equal("Augusta", profile.FirstName);
            Assert.Equal("augusta@example.com", profile.Email);
            Assert.Equal(new DateOnly(1815, 12, 10), profile.DateOfBirth);
            Assert.Equal(2, await db.FormSubmissions.CountAsync());
        }

        [Fact]
        public async Task Submit_ANewRegistrationIsMatchedAgainstIcmContacts_OnTheIdentityAttributes()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":" ada@example.com ","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            ContactMatchCriteria criteria = Assert.Single(_contactMatch.Calls);
            Assert.Equal(
                new ContactMatchCriteria("Ada", "Lovelace", new DateOnly(1815, 12, 10), "ada@example.com", "050082833"),
                criteria);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.NotNull(profile.IcmContactMatchedAt);
        }

        [Fact]
        public async Task Submit_ANewRegistrationWithNoIcmMatchIsStillCreated_WithoutAMatchTime()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            _contactMatch.Result = new ContactMatchResult(IsMatch: false);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Null(profile.IcmContactMatchedAt);
        }

        [Fact]
        public async Task Submit_UpdatingAnExistingProfileDoesNotMatchAgain()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            FormsService service = NewService(db);
            const string answers = """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}""";

            await service.SubmitAsync("registration", Request(3, answers), CancellationToken.None);
            await service.SubmitAsync("registration", Request(3, answers), CancellationToken.None);

            Assert.Single(_contactMatch.Calls);
        }

        [Fact]
        public async Task Submit_ARefusedRegistrationIsNotMatched()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"not-a-date","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Empty(_contactMatch.Calls);
        }

        [Fact]
        public async Task HardcodedContactMatch_AlwaysFindsAMatch()
        {
            ContactMatchResult result = await new HardcodedContactMatchProvider().MatchAsync(
                new ContactMatchCriteria("Ada", "Lovelace", new DateOnly(1815, 12, 10), "ada@example.com", "050082833"),
                CancellationToken.None);

            Assert.True(result.IsMatch);
        }

        [Fact]
        public async Task Submit_WithEmailVerificationOn_AnAddressNotProvenInStep2IsRefused()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            _features = TestFeatures.AllOn();

            FormSubmissionResultModel result = await NewService(db).SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.False(result.IsValid);
            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("email", error.Field);
            Assert.Equal(ValidationKeywords.RegistrationEmailNotVerified, error.Keyword);
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        [Fact]
        public async Task Submit_WithEmailVerificationOn_AProvenAddressRegistersVerified_AndSendsNothing()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            _features = TestFeatures.AllOn();
            // Recent: a proof only counts within the registration session.
            DateTimeOffset provenAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            db.RegistrationEmailVerifications.Add(new RegistrationEmailVerification
            {
                Subject = "test-subject",
                Email = "ada@example.com",
                RequestedAt = provenAt.AddMinutes(-5),
                VerifiedAt = provenAt,
            });
            await db.SaveChangesAsync();

            FormSubmissionResultModel result = await NewService(db).SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":" ADA@example.com ","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = await db.MyssUserProfiles.SingleAsync();
            Assert.Equal("ADA@example.com", profile.Email);
            Assert.Equal(provenAt, profile.EmailVerifiedAt);
            Assert.Null(profile.EmailVerificationTokenHash);
            Assert.Empty(await db.RegistrationEmailVerifications.ToListAsync());
            Assert.Empty(_email.Sent);
        }

        // Copilot review, MYSS-295: an account holder must not be able to swap
        // their email through registration, skipping Account Info's pending
        // change and the notice to the old address.
        [Fact]
        public async Task Submit_WithEmailVerificationOn_AnAccountHolderCannotChangeTheirEmailThroughRegistration()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            _features = TestFeatures.AllOn();
            db.MyssUserProfiles.Add(new MyssUserProfile
            {
                Id = Guid.NewGuid(),
                Subject = "test-subject",
                FirstName = "Ada",
                LastName = "Lovelace",
                DateOfBirth = new DateOnly(1815, 12, 10),
                Email = "ada@example.com",
                Sin = "050082833",
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            // However it got there: a row proving another address.
            db.RegistrationEmailVerifications.Add(new RegistrationEmailVerification
            {
                Subject = "test-subject",
                Email = "attacker@example.com",
                RequestedAt = DateTimeOffset.UtcNow,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            FormSubmissionResultModel result = await NewService(db).SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"attacker@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.Equal(ValidationKeywords.RegistrationEmailNotVerified, Assert.Single(result.Errors).Keyword);
            Assert.Equal("ada@example.com", (await db.MyssUserProfiles.SingleAsync()).Email);
        }

        [Fact]
        public async Task Submit_WithEmailVerificationOn_AnAccountHolderMayRegisterAgainWithTheirOwnAddress()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            db.MyssUserProfiles.Add(new MyssUserProfile
            {
                Id = Guid.NewGuid(),
                Subject = "test-subject",
                FirstName = "Ada",
                LastName = "Lovelace",
                DateOfBirth = new DateOnly(1815, 12, 10),
                Email = "ada@example.com",
                Sin = "050082833",
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            _features = TestFeatures.AllOn();

            FormSubmissionResultModel result = await NewService(db).SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Augusta","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = await db.MyssUserProfiles.SingleAsync();
            Assert.Equal("Augusta", profile.FirstName);
            Assert.Equal("ada@example.com", profile.Email);
            Assert.NotNull(profile.EmailVerifiedAt);
        }

        [Fact]
        public async Task Submit_WithEmailVerificationOn_AnAddressOtherThanTheProvenOneIsRefused()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            _features = TestFeatures.AllOn();
            db.RegistrationEmailVerifications.Add(new RegistrationEmailVerification
            {
                Subject = "test-subject",
                Email = "ada@example.com",
                RequestedAt = DateTimeOffset.UtcNow,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            FormSubmissionResultModel result = await NewService(db).SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"augusta@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.Equal(ValidationKeywords.RegistrationEmailNotVerified, Assert.Single(result.Errors).Keyword);
        }

        // Copilot review, MYSS-295: the flag switched off mid-registration must
        // not leave the registration's email proof behind.
        [Fact]
        public async Task Submit_WithEmailVerificationOff_ARegistrationStillDropsItsEmailProof()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            db.RegistrationEmailVerifications.Add(new RegistrationEmailVerification
            {
                Subject = "test-subject",
                Email = "ada@example.com",
                RequestedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            FormSubmissionResultModel result = await NewService(db).SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Empty(await db.RegistrationEmailVerifications.ToListAsync());
        }

        [Fact]
        public async Task Submit_WithEmailVerificationOffRegistrationStoresTheAddressAndSendsNothing()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            _features = TestFeatures.AllOnExcept(FeatureFlags.EmailVerification);

            await NewService(db).SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":" ada@example.com ","sin":"050082833"}"""),
                CancellationToken.None);

            MyssUserProfile profile = await db.MyssUserProfiles.SingleAsync();
            Assert.Equal("ada@example.com", profile.Email);
            Assert.Null(profile.EmailVerificationTokenHash);
            Assert.Empty(_email.Sent);
        }


        private const string RegistrationSpecV4 = """
        {
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
            { "type": "email", "key": "email", "input": true, "validate": { "required": true } },
            { "type": "phoneNumber", "key": "phone", "input": true, "validate": { "required": true } },
            { "type": "datetime", "key": "dateOfBirth", "input": true, "validate": { "required": true } },
            {
              "type": "panel", "key": "aboutYou", "input": false,
              "components": [
                {
                  "type": "bcgovRadio", "key": "gender", "input": true, "validate": { "required": true },
                  "values": [ { "label": "Man/Boy", "value": "man" }, { "label": "Woman/Girl", "value": "woman" } ]
                }
              ]
            },
            { "type": "textfield", "key": "sin", "input": true, "properties": { "myssValidator": "sin" }, "validate": { "required": true } }
          ]
        }
        """;

        private const string RegistrationAnswersV4 =
            """{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"2505550100","dateOfBirth":"1815-12-10","gender":"woman","sin":"050082833"}""";

        private const string TestBceidGuid = "66666666-6666-6666-6666-666666666666";

        [Fact]
        public async Task Submit_Registration_ABceidCitizenMustCreateAPin()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db, new StubCurrentUserAccessor("test-subject", TestBceidGuid));

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration", Request(4, RegistrationAnswersV4), CancellationToken.None);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("pin", error.Field);
            Assert.Equal(ValidationKeywords.PinInvalidFormat, error.Keyword);
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        [Theory]
        [InlineData("123")]
        [InlineData("12a4")]
        [InlineData("12345")]
        public async Task Submit_Registration_ThePinMustBeFourDigits(string pin)
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db, new StubCurrentUserAccessor("test-subject", TestBceidGuid));

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration", Request(4, RegistrationAnswersV4, pin, pin), CancellationToken.None);

            Assert.Equal(ValidationKeywords.PinInvalidFormat, Assert.Single(result.Errors, e => e.Field == "pin").Keyword);
        }

        [Fact]
        public async Task Submit_Registration_ThePinMustBeTypedTwiceTheSame()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db, new StubCurrentUserAccessor("test-subject", TestBceidGuid));

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration", Request(4, RegistrationAnswersV4, "4821", "4812"), CancellationToken.None);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("pinConfirmation", error.Field);
            Assert.Equal(ValidationKeywords.PinMismatch, error.Keyword);
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        [Fact]
        public async Task Submit_Registration_StoresOnlyTheSaltedHashOfThePin()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db, new StubCurrentUserAccessor("test-subject", TestBceidGuid));

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration", Request(4, RegistrationAnswersV4, "4821", "4821"), CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.DoesNotContain("4821", profile.PinHash!, StringComparison.Ordinal);
            Assert.True(_pinHasher.Verify(profile.PinHash!, Pin.TryCreate("4821").Value!, out _));

            // The answers are stored as submitted, which is why the PIN is not one of them.
            FormSubmission submission = Assert.Single(await db.FormSubmissions.ToListAsync());
            Assert.DoesNotContain("4821", submission.Answers.RootElement.GetRawText(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Submit_Registration_ASignInWithoutAPinMayNotSendOne()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration", Request(4, RegistrationAnswersV4, "4821", "4821"), CancellationToken.None);

            Assert.Equal(ValidationKeywords.PinNotAvailable, Assert.Single(result.Errors).Keyword);
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        [Fact]
        public async Task Submit_Registration_ASignInWithoutAPinRegistersWithoutOne()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration", Request(4, RegistrationAnswersV4), CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Null(Assert.Single(await db.MyssUserProfiles.ToListAsync()).PinHash);
        }

        [Fact]
        public async Task Submit_Registration_RegisteringAgainDoesNotReplaceAPinAlreadySet()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db, new StubCurrentUserAccessor("test-subject", TestBceidGuid));
            await service.SubmitAsync("registration", Request(4, RegistrationAnswersV4, "4821", "4821"), CancellationToken.None);

            FormSubmissionResultModel again = await service.SubmitAsync(
                "registration", Request(4, RegistrationAnswersV4, "9999", "9999"), CancellationToken.None);

            Assert.True(again.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.True(_pinHasher.Verify(profile.PinHash!, Pin.TryCreate("4821").Value!, out _));
        }

        [Fact]
        public async Task Submit_Registration_PutsThePhoneOnAccountInfoAsTheCellNumber()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db);

            await service.SubmitAsync("registration", Request(4, RegistrationAnswersV4), CancellationToken.None);

            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            MyssUserPhone phone = Assert.Single(await db.Set<MyssUserPhone>().ToListAsync());
            Assert.Equal(profile.Id, phone.ProfileId);
            Assert.Equal("2505550100", phone.Number);
            Assert.Equal(AccountPhoneType.Cell, phone.Type);
            Assert.Equal(0, phone.Position);
        }

        [Fact]
        public async Task Submit_Registration_WithoutAPhoneAddsNoneToAccountInfo()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 2, RegistrationSpec);
            FormsService service = NewService(db);

            await service.SubmitAsync(
                "registration",
                Request(2, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Empty(await db.Set<MyssUserPhone>().ToListAsync());
        }

        [Fact]
        public async Task Submit_Registration_RegisteringAgainLeavesTheAccountInfoPhonesAlone()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db);
            await service.SubmitAsync("registration", Request(4, RegistrationAnswersV4), CancellationToken.None);

            FormSubmissionResultModel again = await service.SubmitAsync(
                "registration",
                Request(4, RegistrationAnswersV4.Replace("2505550100", "2505550199", StringComparison.Ordinal)),
                CancellationToken.None);

            Assert.True(again.IsValid);
            Assert.Equal("2505550100", Assert.Single(await db.Set<MyssUserPhone>().ToListAsync()).Number);
        }

                private const string RegistrationSpecV5 = """
                {
                    "components": [
                        { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
                        { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
                        { "type": "email", "key": "email", "input": true, "validate": { "required": true } },
                        { "type": "textfield", "key": "phone", "input": true,
                            "properties": { "myssValidator": "phone" },
                            "validate": { "required": true, "customMessage": "Phone number is invalid" } },
                        { "type": "datetime", "key": "dateOfBirth", "input": true, "validate": { "required": true } },
                        { "type": "bcgovRadio", "key": "gender", "input": true, "validate": { "required": true },
                            "values": [ { "label": "Woman/Girl", "value": "woman" } ] },
                        { "type": "textfield", "key": "sin", "input": true,
                            "properties": { "myssValidator": "sin" }, "validate": { "required": true } }
                    ]
                }
                """;

        [Theory]
        [InlineData("(250) 555-0100")]
        [InlineData("250-555-0100")]
        public async Task Submit_RegistrationV4_PersistsTheNormalizedPhoneAndTheGender(string phone)
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(4, $$"""{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"{{phone}}","dateOfBirth":"1815-12-10","gender":"woman","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Equal("2505550100", profile.Phone);
            Assert.Equal("woman", profile.Gender);
        }

        [Fact]
        public async Task Submit_RegistrationOnAnEarlierVersion_KeepsThePhoneAndGenderV4Stored()
        {
            using FormsDbContext db = NewDb();
            FormsService service = NewService(db);

            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            await service.SubmitAsync(
                "registration",
                Request(4, """{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"2505550100","dateOfBirth":"1815-12-10","gender":"woman","sin":"050082833"}"""),
                CancellationToken.None);

            // An old tab still showing v3, which has no phone or gender field.
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Augusta","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"augusta@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Equal("Augusta", profile.FirstName);
            Assert.Equal("2505550100", profile.Phone);
            Assert.Equal("woman", profile.Gender);
        }

        [Fact]
        public async Task Submit_RegistrationV4_OverAnExistingProfile_StoresThePhoneAndGender()
        {
            using FormsDbContext db = NewDb();
            FormsService service = NewService(db);

            // Registered on v3, which asks for neither.
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 3, RegistrationSpec);
            await service.SubmitAsync(
                "registration",
                Request(3, """{"firstName":"Ada","lastName":"Lovelace","dateOfBirth":"1815-12-10","email":"ada@example.com","sin":"050082833"}"""),
                CancellationToken.None);

            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(4, """{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"(250) 555-0100","dateOfBirth":"1815-12-10","gender":"woman","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Equal("2505550100", profile.Phone);
            Assert.Equal("woman", profile.Gender);
        }

        [Fact]
        public async Task Submit_RegistrationV4_Again_ReplacesThePhoneAndGender()
        {
            using FormsDbContext db = NewDb();
            FormsService service = NewService(db);
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);

            await service.SubmitAsync(
                "registration",
                Request(4, """{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"2505550100","dateOfBirth":"1815-12-10","gender":"woman","sin":"050082833"}"""),
                CancellationToken.None);
            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(4, """{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"6045550199","dateOfBirth":"1815-12-10","gender":"man","sin":"050082833"}"""),
                CancellationToken.None);

            Assert.True(result.IsValid);
            MyssUserProfile profile = Assert.Single(await db.MyssUserProfiles.ToListAsync());
            Assert.Equal("6045550199", profile.Phone);
            Assert.Equal("man", profile.Gender);
        }

        [Theory]
        [InlineData("555-0100")]
        [InlineData("+44 20 7946 0958")]
        [InlineData("+1 250 555 0100")]
        [InlineData("1505550100")]
        public async Task Submit_RegistrationWithInvalidPhone_IsRefused(string phone)
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(4, $$"""{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"{{phone}}","dateOfBirth":"1815-12-10","gender":"woman","sin":"050082833"}"""),
                CancellationToken.None);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("phone", error.Field);
            Assert.Equal(ValidationKeywords.PhoneInvalidFormat, error.Keyword);
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        [Theory]
        [InlineData("(250) 555-0100", true)]
        [InlineData("1505550100", false)]
        [InlineData("", false)]
        public async Task Submit_RegistrationV5_UsesTheSharedPhoneRule(string phone, bool valid)
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 5, RegistrationSpecV5);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(5, $$"""{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"{{phone}}","dateOfBirth":"1815-12-10","gender":"woman","sin":"050082833"}"""),
                CancellationToken.None);

            if (valid)
            {
                Assert.True(result.IsValid);
                Assert.Equal("2505550100", Assert.Single(await db.MyssUserProfiles.ToListAsync()).Phone);
                return;
            }

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("phone", error.Field);
            Assert.Equal(phone == "" ? ValidationKeywords.FieldRequired : ValidationKeywords.PhoneInvalidFormat, error.Keyword);
            Assert.Equal("Phone number is invalid", error.Message);
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        [Fact]
        public async Task Submit_RegistrationWithAGenderTheSpecDoesNotOffer_IsRefused()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 4, RegistrationSpecV4);
            FormsService service = NewService(db);

            FormSubmissionResultModel result = await service.SubmitAsync(
                "registration",
                Request(4, """{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","phone":"2505550100","dateOfBirth":"1815-12-10","gender":"anything","sin":"050082833"}"""),
                CancellationToken.None);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("gender", error.Field);
            Assert.Equal(ValidationKeywords.RegistrationGenderUnknown, error.Keyword);
            Assert.Empty(await db.MyssUserProfiles.ToListAsync());
        }

        private static FormSubmissionRequestModel Request(
            int version,
            string answersJson,
            string? pin = null,
            string? pinConfirmation = null)
        {
            using JsonDocument answers = JsonDocument.Parse(answersJson);
            return new FormSubmissionRequestModel
            {
                FormSpecVersion = version,
                Answers = answers.RootElement.Clone(),
                Pin = pin,
                PinConfirmation = pinConfirmation,
            };
        }

        private FormsService NewService(
            FormsDbContext db,
            ICurrentUserAccessor? currentUserAccessor = null)
        {
            ICurrentUserAccessor caller = currentUserAccessor ?? new StubCurrentUserAccessor("test-subject");
            return new FormsService(
                NullLogger<FormsService>.Instance,
                db,
                _provider,
                caller,
                _errorMessages,
                _pinHasher,
                TestEmailVerification.NewService(db, caller, _email),
                _features,
                _contactMatch);
        }

            private sealed class AnonymousCurrentUserAccessor : ICurrentUserAccessor
            {
                public CurrentUser User => CurrentUser.Anonymous;
            }

        private static FormsDbContext NewDb()
        {
            DbContextOptions<FormsDbContext> options = new DbContextOptionsBuilder<FormsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new InMemoryFormsDbContext(options);
        }
    }
}
