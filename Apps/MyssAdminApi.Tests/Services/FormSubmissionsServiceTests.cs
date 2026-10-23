namespace Myss.AdminApi.Tests.Services
{
    using System.Text;
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Services;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.Data;

    /// <summary>
    /// Tests for <see cref="FormSubmissionsService"/>: the staff reads of stored
    /// submissions, moved from MyssApi's FormsService with the worker endpoints.
    /// </summary>
    public class FormSubmissionsServiceTests
    {
        private readonly FakeFormSpecProvider _provider = new();
        private readonly RecordingPdfProvider _pdfProvider = new();

        [Fact]
        public async Task GetSubmission_FetchesArchivedVersion_NeverLatest()
        {
            // A submission renders from the spec version stored on it, even
            // when a newer version exists.
            using FormsDbContext db = NewDb();
            Guid id = await SeedSubmission(db, "poc-test-form", version: 1);
            _provider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 1);
            _provider.LatestResult = FakeFormSpecProvider.Spec("poc-test-form", 2);
            FormSubmissionsService service = NewService(db);

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
            FormSubmissionsService service = NewService(db);

            FormSubmissionResponseModel? result = await service.GetSubmissionAsync(id, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Null(result.Spec);
            Assert.Equal("Ada", result.Answers.GetProperty("firstName").GetString());
        }

        [Fact]
        public async Task GetSubmission_UnknownId_ReturnsNull()
        {
            using FormsDbContext db = NewDb();
            FormSubmissionsService service = NewService(db);

            FormSubmissionResponseModel? result = await service.GetSubmissionAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.Empty(_provider.VersionCalls);
        }

        [Fact]
        public async Task ListSubmissions_ReturnsNewestFirst_ForTheRequestedFormOnly()
        {
            using FormsDbContext db = NewDb();
            Guid older = await SeedSubmission(db, "poc-test-form", 1, DateTimeOffset.UtcNow.AddHours(-2));
            Guid newer = await SeedSubmission(db, "poc-test-form", 2, DateTimeOffset.UtcNow.AddHours(-1));
            await SeedSubmission(db, "other-form", 1, DateTimeOffset.UtcNow);
            FormSubmissionsService service = NewService(db);

            IReadOnlyList<FormSubmissionSummaryModel> list =
                await service.ListSubmissionsAsync("poc-test-form", CancellationToken.None);

            Assert.Equal([newer, older], list.Select(s => s.Id));
        }

        [Fact]
        public async Task BusPassPdf_ForAnotherForm_IsNotFound_AndNothingIsRendered()
        {
            // The PDF template is the bus pass request form; a submission to any
            // other form must not be poured into it.
            using FormsDbContext db = NewDb();
            Guid id = await SeedSubmission(db, "poc-test-form", version: 1);
            FormSubmissionsService service = NewService(db);

            byte[]? pdf = await service.GetBusPassSubmissionPdfAsync(id, CancellationToken.None);

            Assert.Null(pdf);
            Assert.Equal(0, _pdfProvider.Calls);
        }

        [Fact]
        public async Task BusPassPdf_RendersTheEmbeddedTemplateWithTheStoredAnswers()
        {
            using FormsDbContext db = NewDb();
            Guid id = await SeedSubmission(db, BusPassPdfFieldMap.FormSpecId, version: 1);
            _provider.VersionResult = FakeFormSpecProvider.Spec(BusPassPdfFieldMap.FormSpecId, 1);
            FormSubmissionsService service = NewService(db);

            byte[]? pdf = await service.GetBusPassSubmissionPdfAsync(id, CancellationToken.None);

            Assert.Equal("%PDF-fake"u8.ToArray(), pdf);
            Assert.Equal(1, _pdfProvider.Calls);

            // The real template, embedded in this assembly: an ODT is a zip.
            Assert.Equal("PK"u8.ToArray(), _pdfProvider.LastTemplate![..2]);
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

        private FormSubmissionsService NewService(FormsDbContext db)
        {
            return new FormSubmissionsService(
                NullLogger<FormSubmissionsService>.Instance,
                db,
                _provider,
                _pdfProvider,
                new EmbeddedTemplateProvider());
        }

        private sealed class RecordingPdfProvider : IPdfProvider
        {
            public int Calls { get; private set; }

            public byte[]? LastTemplate { get; private set; }

            public Task<byte[]> GenerateFromOdtAsync(byte[] odtTemplate, object data, CancellationToken cancellationToken)
            {
                Calls++;
                LastTemplate = odtTemplate;
                return Task.FromResult(Encoding.ASCII.GetBytes("%PDF-fake"));
            }
        }
    }
}
