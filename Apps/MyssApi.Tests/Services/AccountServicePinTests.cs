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
    /// Creating and changing the PIN on Account Info (MYSS-258): the checks,
    /// the stored hash, and the lockout after wrong current PINs.
    /// </summary>
    public class AccountServicePinTests
    {
        private const string Subject = "pin-subject";
        private const string BceidGuid = "55555555-5555-5555-5555-555555555555";

        private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        private readonly PinHasher _hasher = new();
        private readonly DbContextOptions<FormsDbContext> _options = new DbContextOptionsBuilder<FormsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        [Fact]
        public async Task ABceidCitizenWithNoPinCanCreateOne_WithoutACurrentPin()
        {
            await Seed(pin: null);

            AccountResultModel result = await Save(current: null, next: "4821", confirm: "4821");

            Assert.Equal(AccountOutcome.Ok, result.Outcome);
            Assert.Equal(AccountPinStatus.Set, result.Account!.PinStatus);
            Assert.True(await StoredPinIs("4821"));
        }

        [Fact]
        public async Task AChangeStoresTheNewPinAsAHash()
        {
            await Seed(pin: "4821");

            AccountResultModel result = await Save(current: "4821", next: "7350", confirm: "7350");

            Assert.Equal(AccountOutcome.Ok, result.Outcome);
            MyssUserProfile profile = await Profile();
            Assert.DoesNotContain("7350", profile.PinHash!, StringComparison.Ordinal);
            Assert.True(await StoredPinIs("7350"));
            Assert.False(await StoredPinIs("4821"));
        }

        [Fact]
        public async Task AChangeNeedsTheCurrentPin()
        {
            await Seed(pin: "4821");

            AccountResultModel result = await Save(current: null, next: "7350", confirm: "7350");

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("currentPin", error.Field);
            Assert.Equal(ValidationKeywords.PinInvalidFormat, error.Keyword);
            Assert.True(await StoredPinIs("4821"));
        }

        [Theory]
        [InlineData("123")]
        [InlineData("12345")]
        [InlineData("12a4")]
        [InlineData("")]
        public async Task ANewPinMustBeFourDigits(string next)
        {
            await Seed(pin: null);

            AccountResultModel result = await Save(current: null, next: next, confirm: next);

            Assert.Equal(AccountOutcome.Invalid, result.Outcome);
            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("newPin", error.Field);
            Assert.Equal(ValidationKeywords.PinInvalidFormat, error.Keyword);
        }

        [Fact]
        public async Task TheNewPinMustBeConfirmed_AndNothingIsSavedWhenItIsNot()
        {
            await Seed(pin: "4821");

            AccountResultModel result = await Save(current: "4821", next: "7350", confirm: "7351");

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("confirmPin", error.Field);
            Assert.Equal(ValidationKeywords.PinMismatch, error.Keyword);
            Assert.True(await StoredPinIs("4821"));
        }

        [Fact]
        public async Task AWrongCurrentPinIsRefusedAndCounted()
        {
            await Seed(pin: "4821");

            AccountResultModel result = await Save(current: "0000", next: "7350", confirm: "7350");

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("currentPin", error.Field);
            Assert.Equal(ValidationKeywords.PinIncorrect, error.Keyword);
            Assert.Equal(1, (await Profile()).PinFailedAttempts);
            Assert.True(await StoredPinIs("4821"));
        }

        [Fact]
        public async Task AMalformedRequestIsNotCountedAsAGuess()
        {
            await Seed(pin: "4821");

            await Save(current: "0000", next: "73", confirm: "73");

            Assert.Equal(0, (await Profile()).PinFailedAttempts);
        }

        [Fact]
        public async Task TheThirdWrongPinInARowLocksChangePinForFifteenMinutes()
        {
            await Seed(pin: "4821");

            await Save(current: "0000", next: "7350", confirm: "7350");
            await Save(current: "0001", next: "7350", confirm: "7350");
            AccountResultModel third = await Save(current: "0002", next: "7350", confirm: "7350");

            Assert.Equal(AccountOutcome.PinLocked, third.Outcome);
            Assert.Equal(_clock.GetUtcNow().AddMinutes(15), third.LockedUntil);
        }

        [Fact]
        public async Task WhileLockedEvenTheRightPinIsRefused()
        {
            await Seed(pin: "4821");
            await LockOut();
            _clock.Advance(TimeSpan.FromMinutes(14));

            AccountResultModel result = await Save(current: "4821", next: "7350", confirm: "7350");

            Assert.Equal(AccountOutcome.PinLocked, result.Outcome);
            Assert.True(await StoredPinIs("4821"));
        }

        [Fact]
        public async Task OnceTheLockoutEndsTheRightPinWorksAgain()
        {
            await Seed(pin: "4821");
            await LockOut();
            _clock.Advance(TimeSpan.FromMinutes(15));

            AccountResultModel result = await Save(current: "4821", next: "7350", confirm: "7350");

            Assert.Equal(AccountOutcome.Ok, result.Outcome);
            MyssUserProfile profile = await Profile();
            Assert.Null(profile.PinLockedUntil);
            Assert.Equal(0, profile.PinFailedAttempts);
        }

        [Fact]
        public async Task AfterALockoutTheCountStartsAgain()
        {
            await Seed(pin: "4821");
            await LockOut();
            _clock.Advance(TimeSpan.FromMinutes(15));

            AccountResultModel result = await Save(current: "0000", next: "7350", confirm: "7350");

            Assert.Equal(AccountOutcome.Invalid, result.Outcome);
            Assert.Equal(1, (await Profile()).PinFailedAttempts);
        }

        [Fact]
        public async Task ARightPinClearsEarlierWrongOnes()
        {
            await Seed(pin: "4821");
            await Save(current: "0000", next: "7350", confirm: "7350");
            await Save(current: "0001", next: "7350", confirm: "7350");

            await Save(current: "4821", next: "7350", confirm: "7350");

            Assert.Equal(0, (await Profile()).PinFailedAttempts);
        }

        [Fact]
        public async Task TheLockoutFollowsConfiguration()
        {
            await Seed(pin: "4821");
            var lockout = new PinLockoutConfig { MaxAttempts = 1, LockoutMinutes = 60 };

            AccountResultModel result = await Save(current: "0000", next: "7350", confirm: "7350", lockout: lockout);

            Assert.Equal(AccountOutcome.PinLocked, result.Outcome);
            Assert.Equal(_clock.GetUtcNow().AddMinutes(60), result.LockedUntil);
        }

        [Fact]
        public async Task ACitizenWhoDoesNotSignInWithBceidHasNoPin()
        {
            await Seed(pin: null);

            AccountResultModel result = await Save(current: null, next: "4821", confirm: "4821", bceidGuid: null);

            Assert.Equal(AccountOutcome.PinNotAvailable, result.Outcome);
            Assert.Null((await Profile()).PinHash);
        }

        [Fact]
        public async Task ACitizenWithNoProfileIsToldToRegister()
        {
            AccountResultModel result = await Save(current: null, next: "4821", confirm: "4821");

            Assert.Equal(AccountOutcome.ProfileRequired, result.Outcome);
        }

        [Theory]
        [InlineData(BceidGuid, null, AccountPinStatus.NotSet)]
        [InlineData(BceidGuid, "4821", AccountPinStatus.Set)]
        [InlineData(null, null, AccountPinStatus.NotApplicable)]
        public async Task TheAccountSaysWhereTheCitizenStandsWithThePin(string? bceidGuid, string? pin, AccountPinStatus expected)
        {
            await Seed(pin);
            using var db = new InMemoryFormsDbContext(_options);

            AccountResultModel result = await NewService(db, bceidGuid, new PinLockoutConfig()).GetAsync(CancellationToken.None);

            Assert.Equal(expected, result.Account!.PinStatus);
        }

        [Fact]
        public void ALockoutThatCouldNeverHappenOrNeverEndIsRefusedAtStartup()
        {
            Assert.Throws<InvalidOperationException>(() => PinLockoutConfig.Validate(new PinLockoutConfig { MaxAttempts = 0 }));
            Assert.Throws<InvalidOperationException>(() => PinLockoutConfig.Validate(new PinLockoutConfig { LockoutMinutes = 0 }));
        }

        private async Task LockOut()
        {
            for (int i = 0; i < 3; i++)
            {
                await Save(current: "0000", next: "7350", confirm: "7350");
            }

            Assert.NotNull((await Profile()).PinLockedUntil);
        }

        private async Task<AccountResultModel> Save(
            string? current,
            string? next,
            string? confirm,
            string? bceidGuid = BceidGuid,
            PinLockoutConfig? lockout = null)
        {
            using var db = new InMemoryFormsDbContext(_options);
            return await NewService(db, bceidGuid, lockout ?? new PinLockoutConfig()).SavePinAsync(
                new SavePinRequestModel { CurrentPin = current, NewPin = next, ConfirmPin = confirm },
                CancellationToken.None);
        }

        private AccountService NewService(FormsDbContext db, string? bceidGuid, PinLockoutConfig lockout) => new(
            NullLogger<AccountService>.Instance,
            db,
            new PlaceholderCaseAccountProvider(),
            new StubCurrentUserAccessor(Subject, bceidGuid),
            _clock,
            _hasher,
            lockout);

        private async Task Seed(string? pin)
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

        private async Task<bool> StoredPinIs(string pin)
        {
            MyssUserProfile profile = await Profile();
            return profile.PinHash is not null && _hasher.Verify(profile.PinHash, Pin.TryCreate(pin).Value!, out _);
        }
    }
}
