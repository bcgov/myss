namespace Myss.Api.Tests.Services
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Data;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// What the in-memory suite cannot prove about a phone update: that a type
    /// kept across the edit is deleted before it is re-inserted, so the real
    /// one-per-type unique index lets the save through.
    /// </summary>
    public class AccountServicePostgresTests
    {
        [PostgresFact]
        public async Task ReplacingAListThatKeepsATypeSatisfiesTheUniqueIndex()
        {
            string subject = $"pg-account-{Guid.NewGuid()}";
            DbContextOptions<FormsDbContext> options = Options();
            using (var setup = new FormsDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.MyssUserProfiles.Add(new MyssUserProfile
                {
                    Id = Guid.NewGuid(),
                    Subject = subject,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    DateOfBirth = new DateOnly(1815, 12, 10),
                    Email = "ada@example.com",
                    Sin = "050082833",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                await setup.SaveChangesAsync();
            }

            try
            {
                await Update(options, subject, ("2505550123", "Home"), ("2505550124", "Work"));
                AccountResultModel result = await Update(options, subject, ("2505550125", "Home"));

                Assert.Equal(AccountOutcome.Ok, result.Outcome);
                using var check = new FormsDbContext(options);
                MyssUserPhone phone = Assert.Single(await check.MyssUserProfiles
                    .Where(p => p.Subject == subject)
                    .SelectMany(p => p.Phones)
                    .ToListAsync());
                Assert.Equal("2505550125", phone.Number);
                Assert.Equal(AccountPhoneType.Home, phone.Type);
            }
            finally
            {
                // The phones go with the profile (cascade).
                using var cleanup = new FormsDbContext(options);
                await cleanup.MyssUserProfiles.Where(p => p.Subject == subject).ExecuteDeleteAsync();
            }
        }

        private static async Task<AccountResultModel> Update(
            DbContextOptions<FormsDbContext> options,
            string subject,
            params (string Number, string Type)[] phones)
        {
            using var db = new FormsDbContext(options);
            var service = new AccountService(
                NullLogger<AccountService>.Instance,
                db,
                new PlaceholderCaseAccountProvider(),
                new StubCurrentUserAccessor(subject),
                TimeProvider.System,
                new PinHasher(),
                new PinLockoutConfig(),
                new FakeErrorMessageProvider());
            return await service.UpdatePhonesAsync(
                new UpdatePhonesRequestModel
                {
                    Phones = [.. phones.Select(p => new PhoneInputModel { Number = p.Number, Type = p.Type })],
                },
                CancellationToken.None);
        }

        private static DbContextOptions<FormsDbContext> Options()
        {
            return new DbContextOptionsBuilder<FormsDbContext>()
                .UseNpgsql(PostgresFactAttribute.ConnectionString)
                .Options;
        }
    }
}
