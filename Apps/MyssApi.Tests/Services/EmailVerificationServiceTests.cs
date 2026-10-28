namespace Myss.Api.Tests.Services
{
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Data;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Changing and proving the email address (MYSS-305): the pending change and
    /// its link, the swap when the link is followed and the notice to the old
    /// address, the unverified address from registration, the cooldown, and the
    /// link's single use and expiry.
    /// </summary>
    public class EmailVerificationServiceTests
    {
        private const string Subject = "email-subject";
        private const string Current = "ada@example.com";
        private const string Next = "ada.lovelace@example.com";

        private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        private readonly FakeEmailProvider _email = new();
        private readonly EmailVerificationConfig _config = new() { TokenLifetimeMinutes = 60, RequestCooldownSeconds = 60 };
        private readonly DbContextOptions<FormsDbContext> _options = new DbContextOptionsBuilder<FormsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        [Fact]
        public async Task AChangeWaitsAsPendingAndEmailsTheNewAddress()
        {
            await Seed(verified: true);

            EmailVerificationResultModel result = await Change(Next);

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            MyssUserProfile profile = await Profile();
            Assert.Equal(Current, profile.Email);
            Assert.Equal(Next, profile.PendingEmail);
            EmailMessage email = Assert.Single(_email.Sent);
            Assert.Equal(Next, email.To);
            Assert.Contains("https://myss.test/verify-email#token=", email.MarkdownBody, StringComparison.Ordinal);
            Assert.Contains("expires in 1 hour", email.MarkdownBody, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-an-address")]
        [InlineData("ada@example")]
        [InlineData("two words@example.com")]
        public async Task AnInvalidAddressIsRefusedAndNothingIsSaved(string address)
        {
            await Seed(verified: true);

            EmailVerificationResultModel result = await Change(address);

            Assert.Equal(EmailVerificationOutcome.Invalid, result.Outcome);
            ValidationErrorModelAssert(result);
            Assert.Null((await Profile()).PendingEmail);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public async Task TheLinkMakesThePendingAddressTheOneInUse_AndTellsTheOldOne()
        {
            await Seed(verified: true);
            await Change(Next);

            EmailVerificationResultModel result = await Verify(_email.LastVerificationToken());

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            MyssUserProfile profile = await Profile();
            Assert.Equal(Next, profile.Email);
            Assert.Null(profile.PendingEmail);
            Assert.Equal(_clock.GetUtcNow(), profile.EmailVerifiedAt);
            Assert.Null(profile.EmailVerificationTokenHash);

            EmailMessage notice = _email.Sent[^1];
            Assert.Equal(Current, notice.To);
            Assert.Contains("was changed", notice.Subject, StringComparison.Ordinal);

            // The old mailbox is not told where the account went.
            Assert.DoesNotContain(Next, notice.MarkdownBody, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TheLinkWorksOnce()
        {
            await Seed(verified: true);
            await Change(Next);
            string token = _email.LastVerificationToken();
            await Verify(token);

            EmailVerificationResultModel again = await Verify(token);

            Assert.Equal(EmailVerificationOutcome.LinkInvalid, again.Outcome);
        }

        [Fact]
        public async Task TheLinkExpiresAfterTheConfiguredTime()
        {
            await Seed(verified: true);
            await Change(Next);
            _clock.Advance(TimeSpan.FromMinutes(60));

            EmailVerificationResultModel result = await Verify(_email.LastVerificationToken());

            Assert.Equal(EmailVerificationOutcome.LinkInvalid, result.Outcome);
            Assert.Equal(Current, (await Profile()).Email);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-guid")]
        [InlineData("0d7ad1a4-38d1-4a6e-9a0e-0f7c5bd3b2e1")]
        public async Task AnUnknownTokenIsRefused(string? token)
        {
            await Seed(verified: true);
            await Change(Next);

            EmailVerificationResultModel result = await Verify(token);

            Assert.Equal(EmailVerificationOutcome.LinkInvalid, result.Outcome);
        }

        [Fact]
        public async Task ASecondChangeInsideTheCooldownSendsNothing_AndKeepsTheFirstLink()
        {
            await Seed(verified: true);
            await Change(Next);
            string first = _email.LastVerificationToken();
            _clock.Advance(TimeSpan.FromSeconds(59));

            EmailVerificationResultModel result = await Change("someone@example.com");

            Assert.Equal(EmailVerificationOutcome.TooSoon, result.Outcome);
            Assert.Equal(_clock.GetUtcNow().AddSeconds(1), result.RetryAt);
            Assert.Single(_email.Sent);
            Assert.Equal(Next, (await Profile()).PendingEmail);
            Assert.Equal(EmailVerificationOutcome.Ok, (await Verify(first)).Outcome);
        }

        [Fact]
        public async Task ANewerChangeKillsTheEarlierLink()
        {
            await Seed(verified: true);
            await Change(Next);
            string first = _email.LastVerificationToken();
            _clock.Advance(TimeSpan.FromSeconds(60));
            await Change("someone@example.com");

            Assert.Equal(EmailVerificationOutcome.LinkInvalid, (await Verify(first)).Outcome);
            Assert.Equal(EmailVerificationOutcome.Ok, (await Verify(_email.LastVerificationToken())).Outcome);
            Assert.Equal("someone@example.com", (await Profile()).Email);
        }

        [Fact]
        public async Task ResendSendsANewLinkForThePendingAddress_AndKillsTheOldOne()
        {
            await Seed(verified: true);
            await Change(Next);
            string first = _email.LastVerificationToken();
            _clock.Advance(TimeSpan.FromSeconds(60));

            EmailVerificationResultModel result = await Resend();

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            Assert.Equal(2, _email.Sent.Count);
            Assert.Equal(Next, _email.Sent[^1].To);
            Assert.Equal(EmailVerificationOutcome.LinkInvalid, (await Verify(first)).Outcome);
        }

        [Fact]
        public async Task ResendObeysTheCooldown()
        {
            await Seed(verified: true);
            await Change(Next);

            EmailVerificationResultModel result = await Resend();

            Assert.Equal(EmailVerificationOutcome.TooSoon, result.Outcome);
            Assert.Single(_email.Sent);
        }

        [Fact]
        public async Task ResendWithNothingToVerifyIsRefused()
        {
            await Seed(verified: true);

            EmailVerificationResultModel result = await Resend();

            Assert.Equal(EmailVerificationOutcome.NothingToVerify, result.Outcome);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public async Task CancelDropsThePendingChangeAndItsLink()
        {
            await Seed(verified: true);
            await Change(Next);
            string token = _email.LastVerificationToken();

            EmailVerificationResultModel result = await Cancel();

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            MyssUserProfile profile = await Profile();
            Assert.Equal(Current, profile.Email);
            Assert.Null(profile.PendingEmail);
            Assert.Equal(EmailVerificationOutcome.LinkInvalid, (await Verify(token)).Outcome);
        }

        [Fact]
        public async Task ChangingBackToTheAddressInUseCancelsThePendingChange_WithoutAnEmail()
        {
            await Seed(verified: true);
            await Change(Next);
            _clock.Advance(TimeSpan.FromSeconds(1));

            EmailVerificationResultModel result = await Change(" ADA@Example.com ");

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            Assert.Null((await Profile()).PendingEmail);
            Assert.Single(_email.Sent);
        }

        [Fact]
        public async Task AnUnverifiedAddressIsReplacedOutright_AndItsLinkTellsNobody()
        {
            await Seed(verified: false);

            await Change(Next);
            MyssUserProfile pending = await Profile();
            Assert.Equal(Next, pending.Email);
            Assert.Null(pending.PendingEmail);

            await Verify(_email.LastVerificationToken());

            MyssUserProfile profile = await Profile();
            Assert.Equal(Next, profile.Email);
            Assert.NotNull(profile.EmailVerifiedAt);
            Assert.Single(_email.Sent);
        }

        [Fact]
        public async Task ResendWorksForAnUnverifiedAddressFromRegistration()
        {
            await Seed(verified: false, tokenSentAt: _clock.GetUtcNow().AddMinutes(-5));

            EmailVerificationResultModel result = await Resend();

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            Assert.Equal(Current, Assert.Single(_email.Sent).To);
        }

        [Fact]
        public async Task CancelLeavesAnUnverifiedAddressFromRegistrationAlone()
        {
            await Seed(verified: false, tokenSentAt: _clock.GetUtcNow());

            await Cancel();

            MyssUserProfile profile = await Profile();
            Assert.Equal(Current, profile.Email);
            Assert.NotNull(profile.EmailVerificationTokenHash);
        }

        [Fact]
        public async Task AnEmailThatCannotBeSentIsReported_AndCanBeResentAfterTheCooldown()
        {
            await Seed(verified: true);
            _email.Unavailable = true;

            EmailVerificationResultModel result = await Change(Next);

            Assert.Equal(EmailVerificationOutcome.EmailNotSent, result.Outcome);
            Assert.Equal(Next, (await Profile()).PendingEmail);

            _email.Unavailable = false;
            _clock.Advance(TimeSpan.FromSeconds(60));
            Assert.Equal(EmailVerificationOutcome.Ok, (await Resend()).Outcome);
        }

        [Fact]
        public async Task WithoutALinkBaseUrlNothingIsSavedOrSent()
        {
            await Seed(verified: true);

            using var db = new InMemoryFormsDbContext(_options);
            EmailVerificationResultModel result = await NewService(db, linkBaseUrl: null)
                .RequestChangeAsync(new ChangeEmailRequestModel { Email = Next }, CancellationToken.None);

            Assert.Equal(EmailVerificationOutcome.EmailNotSent, result.Outcome);
            Assert.Null((await Profile()).PendingEmail);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public async Task ANoticeThatCannotBeSentDoesNotUndoTheChange()
        {
            await Seed(verified: true);
            await Change(Next);
            string token = _email.LastVerificationToken();
            _email.Unavailable = true;

            EmailVerificationResultModel result = await Verify(token);

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            Assert.Equal(Next, (await Profile()).Email);
        }

        [Fact]
        public async Task TwoChangesAtOnceSendOneEmail()
        {
            await Seed(verified: true);

            using var db = new InterruptibleFormsDbContext(_options) { BeforeNextSave = () => Change("someone@example.com") };
            EmailVerificationResultModel result = await NewService(db)
                .RequestChangeAsync(new ChangeEmailRequestModel { Email = Next }, CancellationToken.None);

            Assert.Equal(EmailVerificationOutcome.TooSoon, result.Outcome);
            Assert.Equal("someone@example.com", Assert.Single(_email.Sent).To);
        }

        [Fact]
        public async Task AnAddressLongerThanMailAllowsIsAFieldError()
        {
            await Seed(verified: true);

            EmailVerificationResultModel result = await Change(new string('a', 243) + "@example.com");

            Assert.Equal(EmailVerificationOutcome.Invalid, result.Outcome);
            Assert.Null((await Profile()).PendingEmail);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public async Task RegistrationStep2EmailsALinkBeforeAnyProfileExists()
        {
            EmailVerificationResultModel result = await RequestRegistration(Current);

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            EmailMessage email = Assert.Single(_email.Sent);
            Assert.Equal(Current, email.To);

            // Marked as registration's, so the page sends the citizen back to
            // their open registration rather than anywhere else.
            Assert.Contains("/verify-email?for=registration#token=", email.MarkdownBody, StringComparison.Ordinal);
            RegistrationEmailModel status = await RegistrationStatus();
            Assert.Equal(Current, status.Email);
            Assert.False(status.Verified);
        }

        [Fact]
        public async Task TheRegistrationLinkProvesTheAddress()
        {
            await RequestRegistration(Current);

            EmailVerificationResultModel result = await Verify(_email.LastVerificationToken());

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            Assert.True((await RegistrationStatus()).Verified);
            Assert.Equal(EmailVerificationOutcome.LinkInvalid, (await Verify(_email.LastVerificationToken())).Outcome);
        }

        [Fact]
        public async Task AnAddressProvenInThisRegistrationIsNotSentAnotherLink()
        {
            await RequestRegistration(Current);
            await Verify(_email.LastVerificationToken());
            _clock.Advance(TimeSpan.FromMinutes(5));

            EmailVerificationResultModel result = await RequestRegistration(" ADA@example.com ");

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            Assert.Single(_email.Sent);
            Assert.True((await RegistrationStatus()).Verified);
        }

        [Fact]
        public async Task ADifferentAddressMustBeProvenAfresh()
        {
            await RequestRegistration(Current);
            await Verify(_email.LastVerificationToken());

            EmailVerificationResultModel result = await RequestRegistration(Next);

            Assert.Equal(EmailVerificationOutcome.Ok, result.Outcome);
            RegistrationEmailModel status = await RegistrationStatus();
            Assert.Equal(Next, status.Email);
            Assert.False(status.Verified);
        }

        [Fact]
        public async Task RegistrationLinksObeyTheCooldown_AndTheEarlierLinkStillWorks()
        {
            await RequestRegistration(Current);
            string first = _email.LastVerificationToken();
            _clock.Advance(TimeSpan.FromSeconds(30));

            EmailVerificationResultModel result = await RequestRegistration(Next);

            Assert.Equal(EmailVerificationOutcome.TooSoon, result.Outcome);
            Assert.Single(_email.Sent);
            Assert.Equal(EmailVerificationOutcome.Ok, (await Verify(first)).Outcome);
        }

        [Fact]
        public async Task TheRegistrationLinkExpires()
        {
            await RequestRegistration(Current);
            _clock.Advance(TimeSpan.FromMinutes(60));

            Assert.Equal(EmailVerificationOutcome.LinkInvalid, (await Verify(_email.LastVerificationToken())).Outcome);
            Assert.False((await RegistrationStatus()).Verified);
        }

        [Fact]
        public async Task AnInvalidRegistrationAddressIsAFieldError()
        {
            EmailVerificationResultModel result = await RequestRegistration("not-an-address");

            Assert.Equal(EmailVerificationOutcome.Invalid, result.Outcome);
            Assert.Equal("email", Assert.Single(result.Errors).Field);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public async Task OnlyTheProvenAddressCountsAsVerified()
        {
            await RequestRegistration(Current);
            await Verify(_email.LastVerificationToken());

            using var db = new InMemoryFormsDbContext(_options);
            EmailVerificationService service = NewService(db);
            Assert.NotNull(await service.RegistrationEmailVerifiedAtAsync(" ADA@EXAMPLE.COM", CancellationToken.None));
            Assert.Null(await service.RegistrationEmailVerifiedAtAsync(Next, CancellationToken.None));
        }

        [Fact]
        public async Task AProvenAddressGoesOnTheProfileVerified_AndItsRecordIsDropped()
        {
            await RequestRegistration(Current);
            await Verify(_email.LastVerificationToken());

            using (var db = new InMemoryFormsDbContext(_options))
            {
                EmailVerificationService service = NewService(db);
                DateTimeOffset provenAt = (await service.RegistrationEmailVerifiedAtAsync(Current, CancellationToken.None))!.Value;
                MyssUserProfile profile = NewProfile(verifiedAt: null);
                db.MyssUserProfiles.Add(profile);
                service.ApplyVerifiedRegistrationEmail(profile, Current, provenAt);
                await db.SaveChangesAsync();
            }

            MyssUserProfile saved = await Profile();
            Assert.NotNull(saved.EmailVerifiedAt);
            Assert.Null((await RegistrationStatus()).Email);
        }

        [Fact]
        public void ALifetimeOrCooldownBelowOneIsRefusedAtStartup()
        {
            Assert.Throws<InvalidOperationException>(() =>
                EmailVerificationConfig.Validate(new EmailVerificationConfig { TokenLifetimeMinutes = 0 }));
            Assert.Throws<InvalidOperationException>(() =>
                EmailVerificationConfig.Validate(new EmailVerificationConfig { RequestCooldownSeconds = 0 }));
        }

        private static void ValidationErrorModelAssert(EmailVerificationResultModel result)
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal("email", error.Field);
            Assert.Equal(ValidationKeywords.EmailInvalidFormat, error.Keyword);
        }

        private async Task<EmailVerificationResultModel> Change(string address)
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await NewService(db).RequestChangeAsync(new ChangeEmailRequestModel { Email = address }, CancellationToken.None);
        }

        private async Task<EmailVerificationResultModel> Resend()
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await NewService(db).ResendAsync(CancellationToken.None);
        }

        private async Task<EmailVerificationResultModel> Cancel()
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await NewService(db).CancelChangeAsync(CancellationToken.None);
        }

        private async Task<EmailVerificationResultModel> RequestRegistration(string address)
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await NewService(db).RequestRegistrationEmailAsync(
                new ChangeEmailRequestModel { Email = address },
                CancellationToken.None);
        }

        private async Task<RegistrationEmailModel> RegistrationStatus()
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await NewService(db).GetRegistrationEmailAsync(CancellationToken.None);
        }

        private async Task<EmailVerificationResultModel> Verify(string? token)
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await NewService(db).VerifyAsync(new VerifyEmailRequestModel { Token = token }, CancellationToken.None);
        }

        private EmailVerificationService NewService(FormsDbContext db, bool withLinks = true) =>
            NewService(db, withLinks ? TestEmailVerification.LinkBaseUrl : null);

        private EmailVerificationService NewService(FormsDbContext db, Uri? linkBaseUrl) => new(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailVerificationService>.Instance,
            db,
            new StubCurrentUserAccessor(Subject),
            _email,
            _clock,
            _config,
            new PinResetConfig { LinkBaseUrl = linkBaseUrl },
            new FakeErrorMessageProvider());

        private MyssUserProfile NewProfile(DateTimeOffset? verifiedAt) => new()
        {
            Id = Guid.NewGuid(),
            Subject = Subject,
            FirstName = "Ada",
            LastName = "Lovelace",
            DateOfBirth = new DateOnly(1815, 12, 10),
            Email = Current,
            Sin = "050082833",
            EmailVerifiedAt = verifiedAt,
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow(),
        };

        private async Task Seed(bool verified, DateTimeOffset? tokenSentAt = null)
        {
            using var db = new InMemoryFormsDbContext(_options);
            MyssUserProfile profile = NewProfile(verified ? _clock.GetUtcNow().AddDays(-30) : null);
            if (tokenSentAt is not null)
            {
                profile.EmailVerificationTokenHash = new string('a', 64);
                profile.EmailVerificationRequestedAt = tokenSentAt;
            }

            db.MyssUserProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        private async Task<MyssUserProfile> Profile()
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await db.MyssUserProfiles.AsNoTracking().SingleAsync(p => p.Subject == Subject);
        }
    }
}
