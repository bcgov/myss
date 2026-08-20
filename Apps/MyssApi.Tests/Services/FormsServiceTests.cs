namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Data;
    using Myss.Api.Domain;
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
        public async Task Submit_ResolvesTheClaimedVersion_NeverTheLatest()
        {
            // §7.2, non-negotiable: a citizen part-way through a form when a
            // designer publishes a new version must be validated against the
            // rules they were actually shown.
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 2, SpecWithFirstName);
            _provider.LatestResult = FakeFormSpecProvider.Spec("poc-test-form", 3);
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

            await service.SubmitAsync("poc-test-form", Request(2, """{"firstName":"Grace"}"""), CancellationToken.None);

            Assert.Equal([("poc-test-form", 2)], _provider.VersionCalls);
            Assert.Empty(_provider.LatestCalls);
        }

        [Fact]
        public async Task Submit_StampsTheRenderedVersion_AndPersistsAnswers()
        {
            using FormsDbContext db = NewDb();
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 2, SpecWithFirstName);
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

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
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

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
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

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
            var service = new FormsService(NullLogger<FormsService>.Instance, db, _provider);

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

        private static FormSubmissionRequestModel Request(int version, string answersJson)
        {
            using JsonDocument answers = JsonDocument.Parse(answersJson);
            return new FormSubmissionRequestModel
            {
                FormSpecVersion = version,
                Answers = answers.RootElement.Clone(),
            };
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
    }
}
