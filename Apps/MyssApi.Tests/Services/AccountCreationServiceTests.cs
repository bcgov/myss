namespace Myss.Api.Tests.Services
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Data;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Tests for <see cref="AccountCreationService"/>: each context records
    /// exactly its own answers, and a document is linked only when it is the
    /// caller's, released and not linked already.
    /// </summary>
    public class AccountCreationServiceTests
    {
        private const string Subject = "test-subject";
        private const string BceidGuid = "66666666-6666-6666-6666-666666666666";

        private readonly FakeAttachmentsService _attachments = new();
        private readonly CapturingLogger _logger = new();

        [Fact]
        public async Task Self_RecordsTheContextAlone()
        {
            using FormsDbContext db = NewDb();
            var model = new AccountCreationRequestModel { Context = "self" };

            MyssUserProfile profile = await ValidateAndApplyAsync(db, model);

            Assert.Equal(AccountCreationContext.Self, profile.AccountCreationContext);
            Assert.Null(profile.HelperAuthorized);
            Assert.Empty(await db.AccountRepresentatives.ToListAsync());
            Assert.Empty(await db.AccountDocuments.ToListAsync());
        }

        [Fact]
        public async Task WithHelper_Authorized_RecordsTheAuthorizationAndTheConsentForms()
        {
            using FormsDbContext db = NewDb();
            Guid consent = _attachments.Release(Subject);
            var model = new AccountCreationRequestModel { Context = "withHelper", HelperAuthorized = true, ConsentDocumentIds = [consent] };

            MyssUserProfile profile = await ValidateAndApplyAsync(db, model);

            Assert.Equal(AccountCreationContext.WithHelper, profile.AccountCreationContext);
            Assert.True(profile.HelperAuthorized);
            AccountDocument document = Assert.Single(await db.AccountDocuments.ToListAsync());
            Assert.Equal(consent, document.AttachmentId);
            Assert.Equal(AccountDocumentPurpose.ConsentToDisclosure, document.Purpose);
            Assert.Empty(await db.AccountRepresentatives.ToListAsync());
        }

        [Fact]
        public async Task WithHelper_NotAuthorized_LinksNoDocument()
        {
            using FormsDbContext db = NewDb();
            var model = new AccountCreationRequestModel
            {
                Context = "withHelper",
                HelperAuthorized = false,
                ConsentDocumentIds = [_attachments.Release(Subject)],
            };

            MyssUserProfile profile = await ValidateAndApplyAsync(db, model);

            Assert.False(profile.HelperAuthorized);
            Assert.Empty(await db.AccountDocuments.ToListAsync());
        }

        [Fact]
        public async Task LegalRepresentative_RecordsTheRepresentativeWithTheAgency_AndThePowerOfAttorney()
        {
            using FormsDbContext db = NewDb();
            Guid powerOfAttorney = _attachments.Release(Subject);
            var model = new AccountCreationRequestModel
            {
                Context = "legalRepresentative",
                HelperAuthorized = true,
                Representative = new RepresentativeRequestModel
                {
                    FirstName = "  Pat ",
                    LastName = " Lee",
                    Phone = "(250) 555-0123",
                    Agency = "  Northern Trustee Services  ",
                },
                ConsentDocumentIds = [_attachments.Release(Subject)],
                PowerOfAttorneyDocumentIds = [powerOfAttorney, powerOfAttorney],
            };

            MyssUserProfile profile = await ValidateAndApplyAsync(db, model);

            Assert.Equal(AccountCreationContext.LegalRepresentative, profile.AccountCreationContext);
            Assert.Null(profile.HelperAuthorized);
            AccountRepresentative representative = Assert.Single(await db.AccountRepresentatives.ToListAsync());
            Assert.Equal(profile.Id, representative.ProfileId);
            Assert.Equal(RepresentativeType.LegalRepresentative, representative.Type);
            Assert.Equal("Pat", representative.FirstName);
            Assert.Equal("Lee", representative.LastName);
            Assert.Equal("2505550123", representative.Phone);
            Assert.Equal("Northern Trustee Services", representative.Agency);
            AccountDocument document = Assert.Single(await db.AccountDocuments.ToListAsync());
            Assert.Equal(powerOfAttorney, document.AttachmentId);
            Assert.Equal(AccountDocumentPurpose.PowerOfAttorney, document.Purpose);
        }

        [Fact]
        public async Task LegalRepresentative_WithABlankAgency_StoresNone()
        {
            using FormsDbContext db = NewDb();
            AccountCreationRequestModel model = Representative("legalRepresentative");
            model.Representative!.Agency = "   ";

            await ValidateAndApplyAsync(db, model);

            Assert.Null(Assert.Single(await db.AccountRepresentatives.ToListAsync()).Agency);
        }

        [Fact]
        public async Task ParentForMinor_RecordsAParent_WithoutAnAgencyOrDocuments()
        {
            using FormsDbContext db = NewDb();
            AccountCreationRequestModel model = Representative("parentForMinor");
            model.Representative!.Agency = "Ignored";
            model.PowerOfAttorneyDocumentIds = [_attachments.Release(Subject)];

            await ValidateAndApplyAsync(db, model);

            AccountRepresentative representative = Assert.Single(await db.AccountRepresentatives.ToListAsync());
            Assert.Equal(RepresentativeType.Parent, representative.Type);
            Assert.Null(representative.Agency);
            Assert.Empty(await db.AccountDocuments.ToListAsync());
        }

        [Fact]
        public async Task AnotherOwnersDocument_IsNotFound()
        {
            using FormsDbContext db = NewDb();
            AccountCreationRequestModel model = Representative("legalRepresentative");
            model.PowerOfAttorneyDocumentIds = [_attachments.Release("someone-else")];

            ValidationErrorModel error = Assert.Single(await NewService(db).ValidateAsync(model, CancellationToken.None));

            Assert.Equal(AccountCreationRules.PowerOfAttorneyDocumentsField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationDocumentNotFound, error.Keyword);
        }

        [Fact]
        public async Task ADocumentNotReleased_IsNotFound()
        {
            using FormsDbContext db = NewDb();
            var model = new AccountCreationRequestModel
            {
                Context = "withHelper",
                HelperAuthorized = true,
                ConsentDocumentIds = [_attachments.Release(Subject), Guid.NewGuid()],
            };

            ValidationErrorModel error = Assert.Single(await NewService(db).ValidateAsync(model, CancellationToken.None));

            Assert.Equal(AccountCreationRules.ConsentDocumentsField, error.Field);
            Assert.Equal(ValidationKeywords.RegistrationDocumentNotFound, error.Keyword);
        }

        [Fact]
        public async Task ADocumentLinkedAlready_IsNotFound()
        {
            using FormsDbContext db = NewDb();
            Guid document = _attachments.Release(Subject);
            db.AccountDocuments.Add(new AccountDocument
            {
                Id = Guid.NewGuid(),
                ProfileId = Guid.NewGuid(),
                AttachmentId = document,
                Purpose = AccountDocumentPurpose.PowerOfAttorney,
                AttachedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            AccountCreationRequestModel model = Representative("legalRepresentative");
            model.PowerOfAttorneyDocumentIds = [document];

            ValidationErrorModel error = Assert.Single(await NewService(db).ValidateAsync(model, CancellationToken.None));

            Assert.Equal(ValidationKeywords.RegistrationDocumentNotFound, error.Keyword);
        }

        [Fact]
        public async Task TooManyDocuments_AreNotLookedUp()
        {
            using FormsDbContext db = NewDb();
            AccountCreationRequestModel model = Representative("legalRepresentative");
            model.PowerOfAttorneyDocumentIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();

            ValidationErrorModel error = Assert.Single(await NewService(db).ValidateAsync(model, CancellationToken.None));

            Assert.Equal(ValidationKeywords.RegistrationDocumentTooMany, error.Keyword);
        }

        [Fact]
        public async Task ACallerWithAProfile_GetsNoErrors()
        {
            using FormsDbContext db = NewDb();
            db.MyssUserProfiles.Add(NewProfile());
            await db.SaveChangesAsync();

            Assert.Empty(await NewService(db).ValidateAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task TheLogLine_CarriesNoNameOrPhone()
        {
            using FormsDbContext db = NewDb();
            AccountCreationRequestModel model = Representative("legalRepresentative");
            model.PowerOfAttorneyDocumentIds = [_attachments.Release(Subject)];

            MyssUserProfile profile = await ValidateAndApplyAsync(db, model);

            string line = Assert.Single(_logger.Messages);
            Assert.Contains("LegalRepresentative", line, StringComparison.Ordinal);
            Assert.Contains(profile.Id.ToString(), line, StringComparison.Ordinal);
            Assert.Contains("1 document", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Pat", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Lee", line, StringComparison.Ordinal);
            Assert.DoesNotContain("555", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Northern", line, StringComparison.Ordinal);
        }

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

        private static MyssUserProfile NewProfile() => new()
        {
            Id = Guid.NewGuid(),
            Subject = Subject,
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Sin = "050082833",
        };

        private static FormsDbContext NewDb()
        {
            DbContextOptions<FormsDbContext> options = new DbContextOptionsBuilder<FormsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new InMemoryFormsDbContext(options);
        }

        private async Task<MyssUserProfile> ValidateAndApplyAsync(FormsDbContext db, AccountCreationRequestModel model)
        {
            AccountCreationService service = NewService(db);
            Assert.Empty(await service.ValidateAsync(model, CancellationToken.None));

            MyssUserProfile profile = NewProfile();
            db.MyssUserProfiles.Add(profile);
            service.Apply(profile, model);
            await db.SaveChangesAsync();
            return profile;
        }

        private AccountCreationService NewService(FormsDbContext db) =>
            new(_logger, db, _attachments, new StubCurrentUserAccessor(Subject, BceidGuid));

        private sealed class CapturingLogger : ILogger<AccountCreationService>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception));
        }
    }
}
