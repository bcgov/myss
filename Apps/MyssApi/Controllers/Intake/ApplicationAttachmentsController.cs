namespace Myss.Api.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;
    using Microsoft.FeatureManagement;
    using Microsoft.FeatureManagement.Mvc;
    using Myss.Api.Configuration;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Common.Models;
    using Myss.Data.Intake;

    /// <summary>
    /// Supporting documents on an application (Application for Assistance or
    /// the Income Assistance application): the applicant adds files while the
    /// application is a draft or returned for more information, sees them
    /// afterwards, and workers see the same files on the review screen.
    /// Each file goes through the attachments module (type and size checks,
    /// magic bytes, virus scan) and its row is linked to the application by
    /// <c>submission_id</c>. Only the owner of the application can reach it;
    /// someone else's application is a 404, as on the other intake routes.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/intake/applications/{id:guid}/attachments")]
    [ApiController]
    [FeatureGate(FeatureFlags.Attachments)]
    [FeatureGate(RequirementType.Any, FeatureFlags.Intake, FeatureFlags.ServiceRequests)]
    [Authorize(Policy = MyssPolicies.Client)]
    public class ApplicationAttachmentsController : Controller
    {
        // Same transport cap as AttachmentsController; the real limit is
        // Attachments:MaxSizeBytes, which the service enforces with a 400.
        private const long RequestSizeCeiling = 104_857_600;

        private readonly ILogger<ApplicationAttachmentsController> _logger;
        private readonly IIntakeService _intakeService;
        private readonly IAttachmentsService _attachmentsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationAttachmentsController"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="intakeService">Injected intake service (ownership and status checks).</param>
        /// <param name="attachmentsService">Injected attachments service.</param>
        public ApplicationAttachmentsController(
            ILogger<ApplicationAttachmentsController> logger,
            IIntakeService intakeService,
            IAttachmentsService attachmentsService)
        {
            _logger = logger;
            _intakeService = intakeService;
            _attachmentsService = attachmentsService;
        }

        /// <summary>
        /// Lists the supporting documents on one of the caller's applications,
        /// in the order they were added.
        /// </summary>
        /// <param name="id">The application.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("ListApplicationAttachments")]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<AttachmentResponseModel>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BaseResponseModel<IReadOnlyList<AttachmentResponseModel>>>> List(
            Guid id,
            CancellationToken cancellationToken)
        {
            IntakeOutcome access = await _intakeService.CheckAttachmentAccessAsync(id, change: false, cancellationToken);
            if (access != IntakeOutcome.Ok)
            {
                return NotFound();
            }

            return new BaseResponseModel<IReadOnlyList<AttachmentResponseModel>>
            {
                Payload = await _attachmentsService.ListForSubmissionAsync(id, cancellationToken),
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Adds a supporting document (multipart/form-data field "file") to a
        /// draft or returned application. 400 for empty, too large or wrong
        /// type, 409 when the application can no longer be changed, 422 when
        /// the scan flags the file, 503 when no scan verdict could be obtained.
        /// </summary>
        /// <param name="id">The application.</param>
        /// <param name="file">The uploaded file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost]
        [RequestSizeLimit(RequestSizeCeiling)]
        [RequestFormLimits(MultipartBodyLengthLimit = RequestSizeCeiling)]
        [Produces("application/json")]
        [EndpointName("UploadApplicationAttachment")]
        [ProducesResponseType(typeof(BaseResponseModel<AttachmentResponseModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BaseResponseModel<AttachmentResponseModel>>> Upload(
            Guid id,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            ActionResult? refused = await RefuseChangeAsync(id, cancellationToken);
            if (refused is not null)
            {
                return refused;
            }

            if (file is null)
            {
                return KeywordProblem(
                    StatusCodes.Status400BadRequest,
                    AttachmentErrorKeywords.FileMissing,
                    "The file was not accepted.",
                    "Send the file as the multipart/form-data field 'file'.");
            }

            AttachmentUploadResult result;
            try
            {
                await using var content = file.OpenReadStream();
                result = await _attachmentsService.UploadAsync(
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    content,
                    cancellationToken,
                    submissionId: id,
                    stillAccepting: async ct =>
                        await _intakeService.CheckAttachmentAccessAsync(id, change: true, ct) == IntakeOutcome.Ok);
            }
            catch (VirusScanUnavailableException ex)
            {
                _logger.LogError(ex, "Application attachment refused: no virus-scan verdict available");
                return KeywordProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    AttachmentErrorKeywords.ScanUnavailable,
                    "The file was not accepted.",
                    "The virus scanner is unavailable; the file was not stored. Try again later.");
            }

            if (result.Rejection is AttachmentRejectionReason rejection)
            {
                int status = rejection switch
                {
                    AttachmentRejectionReason.Infected => StatusCodes.Status422UnprocessableEntity,
                    AttachmentRejectionReason.NoLongerAccepted => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest,
                };
                return KeywordProblem(status, rejection.ToKeyword(), "The file was not accepted.", result.Detail);
            }

            return new BaseResponseModel<AttachmentResponseModel>
            {
                Payload = result.Attachment!,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Removes a supporting document from a draft or returned application.
        /// 204 when removed, 404 when there is no such file on it, 409 when
        /// the application can no longer be changed.
        /// </summary>
        /// <param name="id">The application.</param>
        /// <param name="attachmentId">The attachment.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpDelete("{attachmentId:guid}")]
        [EndpointName("DeleteApplicationAttachment")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult> Delete(Guid id, Guid attachmentId, CancellationToken cancellationToken)
        {
            ActionResult? refused = await RefuseChangeAsync(id, cancellationToken);
            if (refused is not null)
            {
                return refused;
            }

            return await _attachmentsService.DeleteFromSubmissionAsync(id, attachmentId, cancellationToken)
                ? NoContent()
                : NotFound();
        }

        private async Task<ActionResult?> RefuseChangeAsync(Guid id, CancellationToken cancellationToken)
        {
            return await _intakeService.CheckAttachmentAccessAsync(id, change: true, cancellationToken) switch
            {
                IntakeOutcome.Ok => null,
                IntakeOutcome.NotEditable => KeywordProblem(
                    StatusCodes.Status409Conflict,
                    IntakeKeywords.NotEditable,
                    "This application can no longer be changed.",
                    "Its current status does not allow adding or removing documents. Reload to see its latest status."),
                _ => NotFound(),
            };
        }

        private ObjectResult KeywordProblem(int status, string keyword, string title, string? detail)
        {
            ObjectResult result = Problem(statusCode: status, title: title, detail: detail);
            ((ProblemDetails)result.Value!).Extensions["keyword"] = keyword;
            return result;
        }
    }
}
