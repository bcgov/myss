namespace Myss.Api.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Configuration;
    using Myss.Api.Controllers;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;

    /// <summary>
    /// Response-shaping tests for the admin actions on <see cref="FormsController"/>:
    /// the <see cref="BaseResponseModel{T}"/> envelope on success, 404 on a missing
    /// draft, and 422 with the error collection on a refused write. Authorization is
    /// covered separately (AuthorizationPolicyTests + ProtectedEndpointTests).
    /// </summary>
    public class FormsControllerAdminTests
    {
        private readonly StubFormsService _service = new();

        [Fact]
        public async Task ListForms_WrapsTheFormsInTheEnvelope()
        {
            _service.Forms =
            [
                new FormSummaryModel
                {
                    FormSpecId = "estimator",
                    Title = "Estimator",
                    Versions = [new FormVersionSummaryModel { Version = 1, IsPublished = true }],
                },
            ];

            ActionResult<BaseResponseModel<IReadOnlyList<FormSummaryModel>>> result =
                await NewController().ListForms(CancellationToken.None);

            Assert.NotNull(result.Value);
            Assert.Single(result.Value!.Payload);
            Assert.Equal("estimator", result.Value.Payload[0].FormSpecId);
        }

        [Fact]
        public async Task GetDraft_Found_WrapsTheSpecInTheEnvelope()
        {
            _service.Draft = new FormSpecModel { FormSpecId = "estimator", Version = 4, Spec = Spec("{}") };

            ActionResult<BaseResponseModel<FormSpecModel>> result =
                await NewController().GetDraft("estimator", CancellationToken.None);

            Assert.NotNull(result.Value);
            Assert.Equal(4, result.Value!.Payload.Version);
        }

        [Fact]
        public async Task GetDraft_Missing_Returns404()
        {
            _service.Draft = null;

            ActionResult<BaseResponseModel<FormSpecModel>> result =
                await NewController().GetDraft("nope", CancellationToken.None);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task SaveDraft_Accepted_WrapsTheSavedSpec()
        {
            _service.SaveResult = FormSpecWriteResultModel<FormSpecModel>.Accepted(
                new FormSpecModel { FormSpecId = "estimator", Version = 4, Spec = Spec("{}") });

            ActionResult<BaseResponseModel<FormSpecModel>> result = await NewController().SaveDraft(
                "estimator",
                new SaveDraftRequestModel { Spec = Spec("{}"), Title = "Estimator" },
                CancellationToken.None);

            Assert.NotNull(result.Value);
            Assert.Equal(4, result.Value!.Payload.Version);
        }

        [Fact]
        public async Task SaveDraft_Refused_Returns422WithTheErrorCollection()
        {
            _service.SaveResult = FormSpecWriteResultModel<FormSpecModel>.Refused(
            [
                new ValidationErrorModel { Field = "dupe", Keyword = "FORMSPEC.COMPONENT_KEY.DUPLICATE", Message = "Duplicate." },
            ]);

            ActionResult<BaseResponseModel<FormSpecModel>> result = await NewController().SaveDraft(
                "estimator",
                new SaveDraftRequestModel { Spec = Spec("{}"), Title = "Estimator" },
                CancellationToken.None);

            var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
            var body = Assert.IsType<BaseResponseModel<IReadOnlyList<ValidationErrorModel>>>(unprocessable.Value);
            Assert.Single(body.Payload);
            Assert.Equal("dupe", body.Payload[0].Field);
        }

        [Fact]
        public async Task Publish_Accepted_WrapsTheNewVersion()
        {
            _service.PublishResult = FormSpecWriteResultModel<PublishResultModel>.Accepted(
                new PublishResultModel { Version = 5 });

            ActionResult<BaseResponseModel<PublishResultModel>> result =
                await NewController().Publish("estimator", CancellationToken.None);

            Assert.NotNull(result.Value);
            Assert.Equal(5, result.Value!.Payload.Version);
        }

        [Fact]
        public async Task Publish_Refused_Returns422()
        {
            _service.PublishResult = FormSpecWriteResultModel<PublishResultModel>.Refused(
            [
                new ValidationErrorModel { Field = "formSpecId", Keyword = "FORMSPEC.PUBLISH.NO_DRAFT", Message = "Nothing to publish." },
            ]);

            ActionResult<BaseResponseModel<PublishResultModel>> result =
                await NewController().Publish("estimator", CancellationToken.None);

            Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
        }

        [Fact]
        public async Task SaveDraft_ContentEngineFailure_Returns502()
        {
            // #1: an infrastructure failure (not a lifecycle refusal) is a 502, not a 422.
            _service.SaveException = new StrapiWriteException(HttpStatusCode.Unauthorized, "bad token");

            ActionResult<BaseResponseModel<FormSpecModel>> result = await NewController().SaveDraft(
                "estimator",
                new SaveDraftRequestModel { Spec = Spec("{}"), Title = "Estimator" },
                CancellationToken.None);

            var obj = Assert.IsAssignableFrom<ObjectResult>(result.Result);
            Assert.Equal(502, obj.StatusCode);
        }

        [Fact]
        public async Task Publish_ContentEngineFailure_Returns502()
        {
            _service.PublishException = new StrapiWriteException(HttpStatusCode.InternalServerError, "boom");

            ActionResult<BaseResponseModel<PublishResultModel>> result =
                await NewController().Publish("estimator", CancellationToken.None);

            var obj = Assert.IsAssignableFrom<ObjectResult>(result.Result);
            Assert.Equal(502, obj.StatusCode);
        }

        [Fact]
        public async Task SaveDraft_UpstreamFailure_Returns502_WithoutLeakingUpstreamDetail()
        {
            // The 502 body must be generic - never Strapi's raw status/body/message.
            _service.SaveException = new StrapiWriteException(HttpStatusCode.InternalServerError, "SECRET-UPSTREAM-BODY");

            ActionResult<BaseResponseModel<FormSpecModel>> result = await NewController().SaveDraft(
                "estimator", new SaveDraftRequestModel { Spec = Spec("{}"), Title = "t" }, CancellationToken.None);

            var obj = Assert.IsAssignableFrom<ObjectResult>(result.Result);
            Assert.Equal(502, obj.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(obj.Value);
            Assert.DoesNotContain("SECRET-UPSTREAM-BODY", problem.Detail ?? string.Empty);
            Assert.DoesNotContain("SECRET-UPSTREAM-BODY", problem.Title ?? string.Empty);
        }

        [Fact]
        public async Task SaveDraft_ContentEngineUnavailable_Returns502()
        {
            _service.SaveException = new ContentEngineUnavailableException("down");

            ActionResult<BaseResponseModel<FormSpecModel>> result = await NewController().SaveDraft(
                "estimator", new SaveDraftRequestModel { Spec = Spec("{}"), Title = "t" }, CancellationToken.None);

            Assert.Equal(502, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        }

        [Fact]
        public async Task Publish_ContentEngineUnavailable_Returns502()
        {
            _service.PublishException = new ContentEngineUnavailableException("down");

            ActionResult<BaseResponseModel<PublishResultModel>> result =
                await NewController().Publish("estimator", CancellationToken.None);

            Assert.Equal(502, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        }

        [Fact]
        public async Task ListForms_ContentEngineUnavailable_Returns502()
        {
            _service.ListFormsException = new ContentEngineUnavailableException("down");

            ActionResult<BaseResponseModel<IReadOnlyList<FormSummaryModel>>> result =
                await NewController().ListForms(CancellationToken.None);

            Assert.Equal(502, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        }

        [Fact]
        public async Task GetDraft_ContentEngineUnavailable_Returns502()
        {
            _service.GetDraftException = new ContentEngineUnavailableException("down");

            ActionResult<BaseResponseModel<FormSpecModel>> result =
                await NewController().GetDraft("estimator", CancellationToken.None);

            Assert.Equal(502, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        }

        [Theory]
        [InlineData(nameof(FormsController.ListForms))]
        [InlineData(nameof(FormsController.GetDraft))]
        [InlineData(nameof(FormsController.SaveDraft))]
        [InlineData(nameof(FormsController.Publish))]
        public void AdminActionsRequireTheAdminIdirPolicy(string actionName)
        {
            // #5: guard against [Authorize(Policy = AdminIdir)] being removed or mistyped
            // on a real action - direct-call tests alone would not catch that.
            var method = typeof(FormsController).GetMethod(actionName);
            Assert.NotNull(method);
            var policies = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>();
            Assert.Contains(policies, a => a.Policy == MyssPolicies.AdminIdir);
        }
        private FormsController NewController() =>
            new(NullLogger<FormsController>.Instance, _service);

        private static JsonElement Spec(string json) => JsonDocument.Parse(json).RootElement.Clone();

        /// <summary>Stub service: admin methods are configurable; the rest are unused here.</summary>
        private sealed class StubFormsService : IFormsService
        {
            public IReadOnlyList<FormSummaryModel> Forms { get; set; } = [];

            public FormSpecModel? Draft { get; set; }

            public FormSpecWriteResultModel<FormSpecModel> SaveResult { get; set; } =
                FormSpecWriteResultModel<FormSpecModel>.Refused([]);

            public FormSpecWriteResultModel<PublishResultModel> PublishResult { get; set; } =
                FormSpecWriteResultModel<PublishResultModel>.Refused([]);

            public Exception? SaveException { get; set; }

            public Exception? PublishException { get; set; }

            public Exception? ListFormsException { get; set; }

            public Exception? GetDraftException { get; set; }

            public Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken) =>
                ListFormsException is not null ? throw ListFormsException : Task.FromResult(Forms);

            public Task<FormSpecModel?> GetDraftOrLatestPublishedAsync(string formSpecId, CancellationToken cancellationToken) =>
                GetDraftException is not null ? throw GetDraftException : Task.FromResult(Draft);

            public Task<FormSpecWriteResultModel<FormSpecModel>> SaveDraftAsync(
                string formSpecId, JsonElement spec, string? title, CancellationToken cancellationToken) =>
                SaveException is not null ? throw SaveException : Task.FromResult(SaveResult);

            public Task<FormSpecWriteResultModel<PublishResultModel>> PublishAsync(
                string formSpecId, CancellationToken cancellationToken) =>
                PublishException is not null ? throw PublishException : Task.FromResult(PublishResult);

            public Task<FormSpecModel?> GetLatestSpecAsync(string formSpecId, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<FormSubmissionResultModel> SubmitAsync(string formSpecId, FormSubmissionRequestModel request, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<FormSubmissionResponseModel?> GetSubmissionAsync(Guid id, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<FormSubmissionResponseModel?> GetBusPassSubmissionForPdfAsync(Guid id, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<byte[]?> GetBusPassSubmissionPdfAsync(Guid id, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<IReadOnlyList<FormSubmissionSummaryModel>> ListSubmissionsAsync(string formSpecId, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }
    }
}
