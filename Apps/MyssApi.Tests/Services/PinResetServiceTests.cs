namespace Myss.Api.Tests.Services
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// Resetting a forgotten PIN by email (MYSS-36): the email and its link, the
    /// single-use token (RULE-IDA-02), the new PIN's checks (RULE-IDA-01) and the
    /// configured expiry.
    /// </summary>
    public class PinResetServiceTests
    {
        private const string Subject = "reset-subject";
        private const string BceidGuid = "66666666-6666-6666-6666-666666666666";

        private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        private readonly PinHasher _hasher = new();
        private readonly FakeEmailProvider _email = new();
        private readonly PinResetConfig _config = new()
        {
            TokenLifetimeMinutes = 30,
            LinkBaseUrl = new Uri("https://myss.test/"),
        };

        private readonly DbContextOptions<FormsDbContext> _options = new DbContextOptionsBuilder<FormsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        [Fact]
        public async Task ARequestEmailsALinkToTheRegisteredAddress()
        {
            await Seed(pin: "4821");

            PinResetResultModel result = await Request();

            Assert.Equal(PinResetOutcome.Ok, result.Outcome);
            Assert.Equal(_clock.GetUtcNow().AddMinutes(30), result.ExpiresAt);
            EmailMessage email = Assert.Single(_email.Sent);
            Assert.Equal("ada@example.com", email.To);
            Assert.Contains("https://myss.test/pin-reset#token=", email.MarkdownBody, StringComparison.Ordinal);
            Assert.Contains("expires in 30 minutes", email.MarkdownBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task OnlyTheTokensHashIsStored()
        {
            await Seed(pin: "4821");

            await Request();

            MyssUserProfile profile = await Profile();
            string token = _email.LastResetToken();
            Assert.NotNull(profile.PinResetTokenHash);
            Assert.Equal(64, profile.PinResetTokenHash!.Length);
            Assert.DoesNotContain(token, profile.PinResetTokenHash, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(_clock.GetUtcNow(), profile.PinResetRequestedAt);
        }

        [Fact]
        public async Task ASignInWithoutAPinCannotRequestAReset()
        {
            await Seed(pin: null);

            PinResetResultModel result = await Request(bceidGuid: null);

            Assert.Equal(PinResetOutcome.PinNotAvailable, result.Outcome);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public async Task ACallerWithNoProfileIsToldToRegister()
        {
            PinResetResultModel result = await Request();

            Assert.Equal(PinResetOutcome.ProfileRequired, result.Outcome);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public async Task AnEmailThatCannotBeSentIsReportedAsSuch()
        {
            await Seed(pin: "4821");
            _email.Unavailable = true;

            PinResetResultModel result = await Request();

            Assert.Equal(PinResetOutcome.EmailNotSent, result.Outcome);
        }

        [Fact]
        public async Task TheLinkSetsANewPinAndClearsALockout()
        {
            await Seed(pin: "4821", lockedUntil: _clock.GetUtcNow().AddMinutes(10), failedAttempts: 2);
            await Request();

            PinResetResultModel result = await Reset(_email.LastResetToken(), "7350", "7350");

            Assert.Equal(PinResetOutcome.Ok, result.Outcome);
            MyssUserProfile profile = await Profile();
            Assert.True(_hasher.Verify(profile.PinHash!, Pin.TryCreate("7350").Value!, out _));
            Assert.Equal(0, profile.PinFailedAttempts);
            Assert.Null(profile.PinLockedUntil);
        }

        [Fact]
        public async Task TheLinkWorksOnce()
        {
            await Seed(pin: "4821");
            await Request();
            string token = _email.LastResetToken();
            await Reset(token, "7350", "7350");

            PinResetResultModel again = await Reset(token, "1111", "1111");

            Assert.Equal(PinResetOutcome.LinkInvalid, again.Outcome);
            MyssUserProfile profile = await Profile();
            Assert.Null(profile.PinResetTokenHash);
            Assert.Null(profile.PinResetRequestedAt);
            Assert.True(_hasher.Verify(profile.PinHash!, Pin.TryCreate("7350").Value!, out _));
        }

        [Fact]
        public async Task ANewerRequestKillsTheEarlierLink()
        {
            await Seed(pin: "4821");
            await Request();
            string first = _email.LastResetToken();
            await Request();

            PinResetResultModel result = await Reset(first, "7350", "7350");

            Assert.Equal(PinResetOutcome.LinkInvalid, result.Outcome);
            Assert.Equal(PinResetOutcome.Ok, (await Reset(_email.LastResetToken(), "7350", "7350")).Outcome);
        }

        [Fact]
        public async Task TheLinkExpiresAfterTheConfiguredTime()
        {
            await Seed(pin: "4821");
            await Request();
            _clock.Advance(TimeSpan.FromMinutes(30));

            PinResetResultModel result = await Reset(_email.LastResetToken(), "7350", "7350");

            Assert.Equal(PinResetOutcome.LinkInvalid, result.Outcome);
            Assert.True(_hasher.Verify((await Profile()).PinHash!, Pin.TryCreate("4821").Value!, out _));
        }

        [Fact]
        public async Task TheLinkStillWorksJustBeforeItExpires()
        {
            await Seed(pin: "4821");
            await Request();
            _clock.Advance(TimeSpan.FromMinutes(29));

            PinResetResultModel result = await Reset(_email.LastResetToken(), "7350", "7350");

            Assert.Equal(PinResetOutcome.Ok, result.Outcome);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-guid")]
        [InlineData("0d7ad1a4-38d1-4a6e-9a0e-0f7c5bd3b2e1")]
        public async Task AnUnknownTokenIsRefused(string? token)
        {
            await Seed(pin: "4821");
            await Request();

            PinResetResultModel result = await Reset(token, "7350", "7350");

            Assert.Equal(PinResetOutcome.LinkInvalid, result.Outcome);
        }

        [Theory]
        [InlineData("123")]
        [InlineData("12345")]
        [InlineData("12a4")]
        [InlineData("")]
        public async Task TheNewPinMustBeFourDigits_AndAMistakeDoesNotUseTheLinkUp(string next)
        {
            await Seed(pin: "4821");
            await Request();
            string token = _email.LastResetToken();

            PinResetResultModel result = await Reset(token, next, next);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("newPin", error.Field);
            Assert.Equal(ValidationKeywords.PinInvalidFormat, error.Keyword);
            Assert.Equal(PinResetOutcome.Ok, (await Reset(token, "7350", "7350")).Outcome);
        }

        [Fact]
        public async Task TheTwoPinsMustMatch()
        {
            await Seed(pin: "4821");
            await Request();

            PinResetResultModel result = await Reset(_email.LastResetToken(), "7350", "7351");

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("confirmPin", error.Field);
            Assert.Equal(ValidationKeywords.PinMismatch, error.Keyword);
        }

        [Fact]
        public async Task TheTokenIsReadWhateverItsCase()
        {
            await Seed(pin: "4821");
            await Request();

            PinResetResultModel result = await Reset(_email.LastResetToken().ToUpperInvariant(), "7350", "7350");

            Assert.Equal(PinResetOutcome.Ok, result.Outcome);
        }

        [Theory]
        [InlineData(60, "1 hour")]
        [InlineData(1440, "24 hours")]
        [InlineData(1, "1 minute")]
        [InlineData(90, "90 minutes")]
        public async Task TheEmailStatesTheLifetime(int minutes, string expected)
        {
            _config.TokenLifetimeMinutes = minutes;
            await Seed(pin: "4821");

            await Request();

            Assert.Contains($"expires in {expected}.", _email.Sent[^1].MarkdownBody, StringComparison.Ordinal);
        }

        [Fact]
        public void ALifetimeBelowOneMinuteIsRefusedAtStartup()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PinResetConfig.Validate(new PinResetConfig { TokenLifetimeMinutes = 0 }));
        }

        [Fact]
        public void ARelativeLinkBaseUrlIsRefusedAtStartup()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PinResetConfig.Validate(new PinResetConfig { LinkBaseUrl = new Uri("/myss", UriKind.Relative) }));
        }

        [Theory]
        [InlineData("http://myss.gov.bc.ca")]
        [InlineData("http://10.0.0.5:5173")]
        [InlineData("ftp://myss.gov.bc.ca")]
        public void ALinkBaseUrlThatIsNotHttpsIsRefusedAtStartup(string url)
        {
            Assert.Throws<InvalidOperationException>(() =>
                PinResetConfig.Validate(new PinResetConfig { LinkBaseUrl = new Uri(url) }));
        }

        [Theory]
        [InlineData("https://myss.gov.bc.ca")]
        [InlineData("http://localhost:5173")]
        [InlineData("http://127.0.0.1:5173")]
        public void AnHttpsOrLocalLinkBaseUrlIsAccepted(string url)
        {
            PinResetConfig.Validate(new PinResetConfig { LinkBaseUrl = new Uri(url) });
        }

        private async Task<PinResetResultModel> Request(string? bceidGuid = BceidGuid)
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await NewService(db, bceidGuid).RequestAsync(CancellationToken.None);
        }

        private async Task<PinResetResultModel> Reset(string? token, string? next, string? confirm)
        {
            using var db = new InMemoryFormsDbContext(_options);

            // The reset is anonymous: no caller to speak of.
            return await NewService(db, bceidGuid: null).ResetAsync(
                new ResetPinRequestModel { Token = token, NewPin = next, ConfirmPin = confirm },
                CancellationToken.None);
        }

        private PinResetService NewService(FormsDbContext db, string? bceidGuid) => new(
            NullLogger<PinResetService>.Instance,
            db,
            new StubCurrentUserAccessor(Subject, bceidGuid),
            _email,
            _clock,
            _hasher,
            _config,
            new FakeErrorMessageProvider());

        private async Task Seed(string? pin, DateTimeOffset? lockedUntil = null, int failedAttempts = 0)
        {
            using var db = new InMemoryFormsDbContext(_options);
            db.MyssUserProfiles.Add(new MyssUserProfile
            {
                Id = Guid.NewGuid(),
                Subject = Subject,
                FirstName = "Ada",
                LastName = "Lovelace",
                DateOfBirth = new DateOnly(1815, 12, 10),
                Email = "ada@example.com",
                Sin = "050082833",
                PinHash = pin is null ? null : _hasher.Hash(Pin.TryCreate(pin).Value!),
                PinFailedAttempts = failedAttempts,
                PinLockedUntil = lockedUntil,
                CreatedAt = _clock.GetUtcNow(),
                UpdatedAt = _clock.GetUtcNow(),
            });
            await db.SaveChangesAsync();
        }

        private async Task<MyssUserProfile> Profile()
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await db.MyssUserProfiles.AsNoTracking().SingleAsync(p => p.Subject == Subject);
        }
    }
}
