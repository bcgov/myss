namespace Myss.Api.Tests.Services
{
    using System;
    using System.Linq;
    using System.Net;
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
    /// Tests for the admin (write) methods on <see cref="FormsService"/>: the
    /// validate-then-write policy and how a Strapi refusal is surfaced.
    /// </summary>
    public class FormsServiceAdminTests
    {
        private readonly FakeFormSpecAdminProvider _admin = new();

        [Fact]
        public async Task SaveDraft_InvalidSpec_ReturnsErrors_AndNeverCallsStrapi()
        {
            // A spec with duplicate keys must be refused before anything is written.
            FormSpecWriteResultModel<FormSpecModel> result = await NewService().SaveDraftAsync(
                "estimator",
                Spec("""{ "components": [ { "key": "dupe" }, { "key": "dupe" } ] }"""),
                "Estimator",
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(FormSpecStructureKeywords.ComponentKeyDuplicate, result.Errors.Select(e => e.Keyword));
            Assert.Empty(_admin.SaveDraftCalls);
        }

        [Fact]
        public async Task SaveDraft_ValidSpec_WritesThroughTheProvider()
        {
            _admin.Saved = new FormSpecModel
            {
                FormSpecId = "estimator",
                Version = 4,
                Title = "Estimator",
                Spec = Spec("{}"),
            };

            FormSpecWriteResultModel<FormSpecModel> result = await NewService().SaveDraftAsync(
                "estimator",
                Spec("""{ "components": [ { "key": "firstName" } ] }"""),
                "Estimator",
                CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(4, result.Value!.Version);
            Assert.Single(_admin.SaveDraftCalls);
            Assert.Equal(("estimator", "Estimator"), _admin.SaveDraftCalls[0]);
        }

        [Fact]
        public async Task SaveDraft_ApplicationErrorWithoutRecognizedKeyword_Propagates()
        {
            // A 400 ApplicationError with no recognized lifecycle keyword is not a trusted
            // refusal - it must propagate (=> 502), not surface an arbitrary message as 422.
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "name": "ApplicationError", "message": "Something else" } }""");

            await Assert.ThrowsAsync<StrapiWriteException>(() => NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None));
        }

        [Fact]
        public async Task Publish_ValidDraft_PublishesAndReturnsVersion()
        {
            _admin.Draft = new FormSpecModel
            {
                FormSpecId = "estimator",
                Version = 4,
                Title = "Estimator",
                Spec = Spec("""{ "components": [ { "key": "firstName" } ] }"""),
            };
            _admin.PublishedVersion = 4;

            FormSpecWriteResultModel<PublishResultModel> result = await NewService().PublishAsync(
                "estimator", CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(4, result.Value!.Version);
            Assert.Equal(["estimator"], _admin.PublishCalls);
        }

        [Fact]
        public async Task Publish_DraftFailsStructure_RefusesWithoutPublishing()
        {
            _admin.Draft = new FormSpecModel
            {
                FormSpecId = "estimator",
                Version = 4,
                Title = "Estimator",
                Spec = Spec("""{ "components": [ { "key": "dupe" }, { "key": "dupe" } ] }"""),
            };

            FormSpecWriteResultModel<PublishResultModel> result = await NewService().PublishAsync(
                "estimator", CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(FormSpecStructureKeywords.ComponentKeyDuplicate, result.Errors.Select(e => e.Keyword));
            Assert.Empty(_admin.PublishCalls);
        }

        [Fact]
        public async Task Publish_NoInProgressDraft_RefusesWithNothingToPublish()
        {
            _admin.Draft = null;
            _admin.PublishException = new NoDraftToPublishException("estimator");

            FormSpecWriteResultModel<PublishResultModel> result = await NewService().PublishAsync(
                "estimator", CancellationToken.None);

            Assert.False(result.IsValid);
            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal(FormSpecStructureKeywords.NothingToPublish, error.Keyword);
        }

        [Fact]
        public async Task ListForms_DelegatesToTheProvider()
        {
            _admin.Forms =
            [
                new FormSummaryModel
                {
                    FormSpecId = "estimator",
                    Title = "Estimator",
                    Versions = [new FormVersionSummaryModel { Version = 1, IsPublished = true }],
                },
            ];

            var forms = await NewService().ListFormsAsync(CancellationToken.None);

            Assert.Single(forms);
            Assert.Equal("estimator", forms[0].FormSpecId);
        }

        [Fact]
        public async Task SaveDraft_LifecycleRefusal_PreservesStrapiKeywords()
        {
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "name": "ApplicationError", "message": "Duplicate component key.", "details": { "keywords": ["FORMSPEC.COMPONENT_KEY.DUPLICATE"] } } }""");

            FormSpecWriteResultModel<FormSpecModel> result = await NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None);

            Assert.False(result.IsValid);
            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("FORMSPEC.COMPONENT_KEY.DUPLICATE", error.Keyword);
            Assert.Contains("Duplicate component key", error.Message);
        }

        [Fact]
        public async Task SaveDraft_MultipleLifecycleKeywords_BecomeMultipleErrors()
        {
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "name": "ApplicationError", "message": "Two problems.", "details": { "keywords": ["FORMSPEC.COMPONENT_KEY.DUPLICATE", "FORMSPEC.CONDITIONAL.UNKNOWN_FIELD"] } } }""");

            FormSpecWriteResultModel<FormSpecModel> result = await NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None);

            Assert.Equal(
                new[] { "FORMSPEC.COMPONENT_KEY.DUPLICATE", "FORMSPEC.CONDITIONAL.UNKNOWN_FIELD" },
                result.Errors.Select(e => e.Keyword));
        }

        [Theory]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("null")]
        [InlineData("\"Forbidden\"")]
        [InlineData("{ \"error\": \"Forbidden\" }")]
        [InlineData("{ \"error\": { \"name\": \"ValidationError\" } }")]
        [InlineData("not valid json {")]
        public async Task SaveDraft_Unrecognized400_PropagatesAsUpstreamFailure(string body)
        {
            // #B: a 400 that is not the lifecycle ApplicationError envelope is an upstream
            // fault, not a validation refusal - it must propagate (controller => 502), never
            // become a 422 with an arbitrary message. Also proves the envelope check is
            // shape-safe (no InvalidOperationException on odd bodies).
            _admin.SaveDraftException = new StrapiWriteException(HttpStatusCode.BadRequest, body);

            await Assert.ThrowsAsync<StrapiWriteException>(() => NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None));
        }

        [Fact]
        public async Task SaveDraft_ApplicationErrorWithMalformedKeywords_Propagates()
        {
            // Recognized envelope name but keywords is the wrong shape => no recognized
            // keyword => treated as an upstream failure, not a 422.
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "name": "ApplicationError", "message": "Refused.", "details": { "keywords": "not-an-array" } } }""");

            await Assert.ThrowsAsync<StrapiWriteException>(() => NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None));
        }

        [Fact]
        public async Task SaveDraft_DuplicateLifecycleKeywords_AreDeduplicated()
        {
            // #D: form-spec-rules can emit the same keyword more than once.
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "name": "ApplicationError", "message": "Bad.", "details": { "keywords": ["FORMSPEC.COMPONENT_KEY.MISSING", "FORMSPEC.COMPONENT_KEY.MISSING", "FORMSPEC.COMPONENT_KEY.DUPLICATE"] } } }""");

            FormSpecWriteResultModel<FormSpecModel> result = await NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None);

            Assert.Equal(
                new[] { "FORMSPEC.COMPONENT_KEY.MISSING", "FORMSPEC.COMPONENT_KEY.DUPLICATE" },
                result.Errors.Select(e => e.Keyword));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        public async Task SaveDraft_InfrastructureFailure_Propagates(HttpStatusCode status)
        {
            // #1: not a lifecycle refusal, so it must NOT be dressed up as a 422.
            _admin.SaveDraftException = new StrapiWriteException(status, "upstream boom");

            await Assert.ThrowsAsync<StrapiWriteException>(() => NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None));
        }

        [Fact]
        public async Task Publish_InfrastructureFailure_Propagates()
        {
            _admin.Draft = new FormSpecModel { FormSpecId = "estimator", Version = 4, Spec = Spec("""{ "components": [ { "key": "firstName" } ] }""") };
            _admin.PublishException = new StrapiWriteException(HttpStatusCode.InternalServerError, "boom");

            await Assert.ThrowsAsync<StrapiWriteException>(() => NewService().PublishAsync("estimator", CancellationToken.None));
        }
        [Fact]
        public async Task SaveDraft_EmptyKeywordsArray_Propagates()
        {
            // ApplicationError with an empty keywords array => no recognized keyword => 502.
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "name": "ApplicationError", "message": "x", "details": { "keywords": [] } } }""");

            await Assert.ThrowsAsync<StrapiWriteException>(() => NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None));
        }

        [Fact]
        public async Task SaveDraft_KeywordsWithoutApplicationErrorName_Propagates()
        {
            // Recognized keywords but the envelope is not named ApplicationError => not a
            // trusted lifecycle refusal.
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "message": "x", "details": { "keywords": ["FORMSPEC.COMPONENT_KEY.DUPLICATE"] } } }""");

            await Assert.ThrowsAsync<StrapiWriteException>(() => NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None));
        }

        [Fact]
        public async Task SaveDraft_UnknownKeywordsOnly_Propagates()
        {
            // ApplicationError but every keyword is outside the authoritative vocabulary.
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "name": "ApplicationError", "message": "x", "details": { "keywords": ["FOO.BAR", "BAZ.QUX"] } } }""");

            await Assert.ThrowsAsync<StrapiWriteException>(() => NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None));
        }

        [Fact]
        public async Task SaveDraft_MixedKeywords_ForwardsOnlyRecognized()
        {
            _admin.SaveDraftException = new StrapiWriteException(
                HttpStatusCode.BadRequest,
                """{ "error": { "name": "ApplicationError", "message": "x", "details": { "keywords": ["FORMSPEC.COMPONENT_KEY.DUPLICATE", "FOO.BAR"] } } }""");

            FormSpecWriteResultModel<FormSpecModel> result = await NewService().SaveDraftAsync(
                "estimator", Spec("""{ "components": [ { "key": "firstName" } ] }"""), "Estimator", CancellationToken.None);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("FORMSPEC.COMPONENT_KEY.DUPLICATE", error.Keyword);
        }

        [Fact]
        public async Task Publish_UnrelatedInvalidOperationException_Propagates()
        {
            // The service catches only NoDraftToPublishException; an unrelated
            // InvalidOperationException (e.g. from parsing) must not be swallowed as no-draft.
            _admin.Draft = new FormSpecModel { FormSpecId = "estimator", Version = 4, Spec = Spec("""{ "components": [ { "key": "firstName" } ] }""") };
            _admin.PublishException = new InvalidOperationException("boom");

            await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().PublishAsync("estimator", CancellationToken.None));
        }

        private FormsService NewService() => new(
            NullLogger<FormsService>.Instance,
            NewDb(),
            new FakeFormSpecProvider(),
            new UnexpectedPdfProvider(),
            new UnexpectedTemplateProvider(),
            _admin);

        private static JsonElement Spec(string json) => JsonDocument.Parse(json).RootElement.Clone();

        private static FormsDbContext NewDb() => new InMemoryFormsDbContext(
            new DbContextOptionsBuilder<FormsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        private sealed class UnexpectedPdfProvider : IPdfProvider
        {
            public Task<byte[]> GenerateFromOdtAsync(byte[] odtTemplate, object data, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("PDF generation is not expected in these tests.");
        }

        private sealed class UnexpectedTemplateProvider : ITemplateProvider
        {
            public Task<byte[]> GetTemplateAsync(string templateName, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Template retrieval is not expected in these tests.");
        }
    }
}
