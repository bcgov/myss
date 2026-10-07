namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;

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
        private readonly IPdfProvider _pdfProvider = new UnexpectedPdfProvider();
        private readonly FakeFormSpecAdminProvider _adminProvider = new();
        private readonly ITemplateProvider _templateProvider = new UnexpectedTemplateProvider();
        private readonly FakeErrorMessageProvider _errorMessages = new();

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
        public async Task GetSubmission_FetchesArchivedVersion_NeverLatest()
        {
            // A submission renders from the spec version stored on it, even
            // when a newer version exists.
            using FormsDbContext db = NewDb();
            Guid id = await SeedSubmission(db, "poc-test-form", version: 1);
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 1);
            _provider.LatestResult = FakeFormSpecProvider.Spec("poc-test-form", 2);
            FormsService service = NewService(db);

            FormSubmissionResponseModel? result = await service.GetSubmissionAsync(id, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal([("poc-test-form", 1)], _provider.VersionCalls);
            Assert.Empty(_provider.LatestCalls);
            Assert.Equal(1, result.Spec!.Version);
        }

        [Fact]
        public async Task GetSubmission_ArchivedVersionGone_ReturnsSubmissionWithoutSpec()
        {
            // The submission should still come back when the content engine
            // no longer has the spec version.
            using FormsDbContext db = NewDb();
            Guid id = await SeedSubmission(db, "poc-test-form", version: 1);
            _provider.VersionResult = null;
            FormsService service = NewService(db);

            FormSubmissionResponseModel? result = await service.GetSubmissionAsync(id, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Null(result.Spec);
            Assert.Equal("Ada", result.Answers.GetProperty("firstName").GetString());
        }

        [Fact]
        public async Task GetSubmission_UnknownId_ReturnsNull()
        {
            using FormsDbContext db = NewDb();
            FormsService service = NewService(db);

            FormSubmissionResponseModel? result = await service.GetSubmissionAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.Empty(_provider.VersionCalls);
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

        [Fact]
        public async Task ListSubmissions_ReturnsNewestFirst_ForTheRequestedFormOnly()
        {
            using FormsDbContext db = NewDb();
            Guid older = await SeedSubmission(db, "poc-test-form", 1, DateTimeOffset.UtcNow.AddHours(-2));
            Guid newer = await SeedSubmission(db, "poc-test-form", 2, DateTimeOffset.UtcNow.AddHours(-1));
            await SeedSubmission(db, "other-form", 1, DateTimeOffset.UtcNow);
            FormsService service = NewService(db);

            IReadOnlyList<FormSubmissionSummaryModel> list =
                await service.ListSubmissionsAsync("poc-test-form", CancellationToken.None);

            Assert.Equal([newer, older], list.Select(s => s.Id));
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

        [Theory]
        [InlineData("(250) 555-0100")]
        [InlineData("250-555-0100")]
        [InlineData("+1 250 555 0100")]
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
        public async Task Submit_RegistrationWithAPhoneThatIsNotTenDigits_IsRefused(string phone)
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

        private static FormSubmissionRequestModel Request(int version, string answersJson)
        {
            using JsonDocument answers = JsonDocument.Parse(answersJson);
            return new FormSubmissionRequestModel
            {
                FormSpecVersion = version,
                Answers = answers.RootElement.Clone(),
            };
        }

        private FormsService NewService(
            FormsDbContext db,
            ICurrentUserAccessor? currentUserAccessor = null)
        {
            return new FormsService(
                NullLogger<FormsService>.Instance,
                db,
                _provider,
                _pdfProvider,
                _templateProvider,
                _adminProvider,
                currentUserAccessor ?? new StubCurrentUserAccessor("test-subject"),
                _errorMessages);
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

        private static async Task<Guid> SeedSubmission(
            FormsDbContext db,
            string formSpecId,
            int version,
            DateTimeOffset? submittedAt = null)
        {
            var submission = new FormSubmission
            {
                Id = Guid.NewGuid(),
                FormSpecId = formSpecId,
                FormSpecVersion = version,
                Answers = JsonDocument.Parse("""{"firstName":"Ada"}"""),
                SubmittedAt = submittedAt ?? DateTimeOffset.UtcNow,
            };
            db.FormSubmissions.Add(submission);
            await db.SaveChangesAsync();
            return submission.Id;
        }

        private sealed class UnexpectedPdfProvider : IPdfProvider
        {
            public Task<byte[]> GenerateFromOdtAsync(
                byte[] odtTemplate,
                object data,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("PDF generation is not expected in these tests.");
            }
        }

        private sealed class UnexpectedTemplateProvider : ITemplateProvider
        {
            public Task<byte[]> GetTemplateAsync(string templateName, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Template retrieval is not expected in these tests.");
            }
        }
    }
}
