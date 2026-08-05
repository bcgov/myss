namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Data;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// Tests for <see cref="FormsService"/>.
    /// </summary>
    public class FormsServiceTests
    {
        private readonly FakeFormSpecProvider _provider = new();

        [Fact]
        public async Task GetSubmission_FetchesArchivedVersion_NeverLatest()
        {
            // A submission renders from the spec version stored on it, even
            // when a newer version exists.
            using FormsDbContext db = NewDb();
            Guid id = await SeedSubmission(db, "poc-test-form", version: 1);
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 1);
            _provider.LatestResult = FakeFormSpecProvider.Spec("poc-test-form", 2);
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

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
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

            FormSubmissionResponseModel? result = await service.GetSubmissionAsync(id, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Null(result.Spec);
            Assert.Equal("Ada", result.Answers.GetProperty("firstName").GetString());
        }

        [Fact]
        public async Task GetSubmission_UnknownId_ReturnsNull()
        {
            using FormsDbContext db = NewDb();
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

            FormSubmissionResponseModel? result = await service.GetSubmissionAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.Empty(_provider.VersionCalls);
        }

        [Fact]
        public async Task Submit_StampsTheRenderedVersion_AndPersistsAnswers()
        {
            using FormsDbContext db = NewDb();
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);
            using JsonDocument answers = JsonDocument.Parse("""{"firstName":"Grace","monthlyIncome":2000}""");
            var request = new FormSubmissionRequestModel
            {
                FormSpecVersion = 2,
                Answers = answers.RootElement.Clone(),
            };

            FormSubmissionResponseModel stored = await service.SubmitAsync("poc-test-form", request, CancellationToken.None);

            FormSubmission row = Assert.Single(await db.FormSubmissions.ToListAsync());
            Assert.Equal(stored.Id, row.Id);
            Assert.Equal("poc-test-form", row.FormSpecId);
            Assert.Equal(2, row.FormSpecVersion);
            Assert.Equal("Grace", row.Answers.RootElement.GetProperty("firstName").GetString());
            Assert.True(stored.SubmittedAt <= DateTimeOffset.UtcNow);
            Assert.True(stored.SubmittedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task ListSubmissions_ReturnsNewestFirst_ForTheRequestedFormOnly()
        {
            using FormsDbContext db = NewDb();
            Guid older = await SeedSubmission(db, "poc-test-form", 1, DateTimeOffset.UtcNow.AddHours(-2));
            Guid newer = await SeedSubmission(db, "poc-test-form", 2, DateTimeOffset.UtcNow.AddHours(-1));
            await SeedSubmission(db, "other-form", 1, DateTimeOffset.UtcNow);
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

            IReadOnlyList<FormSubmissionSummaryModel> list =
                await service.ListSubmissionsAsync("poc-test-form", CancellationToken.None);

            Assert.Equal([newer, older], list.Select(s => s.Id));
        }

        private static FormsDbContext NewDb()
        {
            DbContextOptions<FormsDbContext> options = new DbContextOptionsBuilder<FormsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new InMemoryFormsDbContext(options);
        }

        /// <summary>
        /// The InMemory provider cannot map <see cref="JsonDocument"/> (Npgsql
        /// can), so tests store it as a raw JSON string instead.
        /// </summary>
        private sealed class InMemoryFormsDbContext : FormsDbContext
        {
            public InMemoryFormsDbContext(DbContextOptions<FormsDbContext> options)
                : base(options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                modelBuilder.Entity<FormSubmission>()
                    .Property(s => s.Answers)
                    .HasConversion(
                        doc => doc.RootElement.GetRawText(),
                        text => JsonDocument.Parse(text, default(JsonDocumentOptions)));
            }
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
    }
}
