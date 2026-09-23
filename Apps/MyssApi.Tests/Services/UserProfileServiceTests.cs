namespace Myss.Api.Tests.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Data;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// Tests for <see cref="UserProfileService"/>.
    /// </summary>
    public class UserProfileServiceTests
    {
        [Fact]
        public async Task HasProfileAsync_ReturnsFalseWhenSubjectHasNoProfile()
        {
            using FormsDbContext db = NewDb();
            var service = new UserProfileService(db);

            bool hasProfile = await service.HasProfileAsync("missing-subject", CancellationToken.None);

            Assert.False(hasProfile);
            Assert.Null(await service.GetFirstNameAsync("missing-subject", CancellationToken.None));
        }

        [Fact]
        public async Task ProfileQueries_ReturnTheStoredProfileForMatchingSubject()
        {
            using FormsDbContext db = NewDb();
            db.MyssUserProfiles.Add(new MyssUserProfile
            {
                Id = Guid.NewGuid(),
                Subject = "registered-subject",
                FirstName = "Ada",
                LastName = "Lovelace",
                DateOfBirth = new DateOnly(1815, 12, 10),
                Email = "ada@example.com",
                Sin = "050082833",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            var service = new UserProfileService(db);

            bool hasProfile = await service.HasProfileAsync("registered-subject", CancellationToken.None);
            string? firstName = await service.GetFirstNameAsync("registered-subject", CancellationToken.None);

            Assert.True(hasProfile);
            Assert.Equal("Ada", firstName);
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
