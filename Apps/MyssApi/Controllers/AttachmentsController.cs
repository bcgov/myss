namespace Myss.Api.Controllers
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
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;

    /// <summary>
    /// The attachments controller: virus-scanned file uploads into object
    /// storage. Protected: attachments always belong to the authenticated
    /// caller. Failures carry a stable dotted keyword (e.g.
    /// <c>DOC.UPLOAD.TYPE_NOT_ALLOWED</c>) in the ProblemDetails
    /// <c>keyword</c> field — that's what the frontend should match on.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/attachments")]
    [ApiController]
    [Authorize]
    public class AttachmentsController : Controller
    {
        // Hard transport cap, matching clamd's StreamMaxLength (100M). The
        // real limit is Attachments:MaxSizeBytes, which the service enforces
        // with a proper 400.
        private const long RequestSizeCeiling = 104_857_600;

        private readonly ILogger<AttachmentsController> _logger;
        private readonly IAttachmentsService _attachmentsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttachmentsController"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="attachmentsService">Injected Attachments Service.</param>
        public AttachmentsController(
            ILogger<AttachmentsController> logger,
            IAttachmentsService attachmentsService)
        {
            _logger = logger;
            _attachmentsService = attachmentsService;
        }

        /// <summary>
        /// Accepts a file (multipart/form-data field "file"), scans it and
        /// stores it for the caller. 400 for empty/too large/wrong type, 422
        /// when the scan flags it, 503 when no scan verdict could be obtained.
        /// A file is never stored without a clean verdict.
        /// </summary>
        /// <param name="file">The uploaded file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost]
        [RequestSizeLimit(RequestSizeCeiling)]
        [RequestFormLimits(MultipartBodyLengthLimit = RequestSizeCeiling)]
        [Produces("application/json")]
        [EndpointName("UploadAttachment")]
        [ProducesResponseType(typeof(BaseResponseModel<AttachmentResponseModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BaseResponseModel<AttachmentResponseModel>>> Upload(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null)
            {
                return KeywordProblem(
                    StatusCodes.Status400BadRequest,
                    AttachmentErrorKeywords.FileMissing,
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
                    cancellationToken);
            }
            catch (VirusScanUnavailableException ex)
            {
                _logger.LogError(ex, "Upload refused: no virus-scan verdict available");
                return KeywordProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    AttachmentErrorKeywords.ScanUnavailable,
                    "The virus scanner is unavailable; the file was not stored. Try again later.");
            }

            if (result.Rejection is AttachmentRejectionReason rejection)
            {
                int status = rejection == AttachmentRejectionReason.Infected
                    ? StatusCodes.Status422UnprocessableEntity
                    : StatusCodes.Status400BadRequest;
                return KeywordProblem(status, rejection.ToKeyword(), result.Detail);
            }

            return new BaseResponseModel<AttachmentResponseModel>
            {
                Payload = result.Attachment!,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Lists the caller's released attachments, newest first. Never
        /// returns anyone else's.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("ListAttachments")]
        [ProducesResponseType(
            typeof(BaseResponseModel<IReadOnlyList<AttachmentResponseModel>>),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<BaseResponseModel<IReadOnlyList<AttachmentResponseModel>>>> List(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttachmentResponseModel> attachments =
                await _attachmentsService.ListOwnAsync(cancellationToken);
            return new BaseResponseModel<IReadOnlyList<AttachmentResponseModel>>
            {
                Payload = attachments,
                DatetimeRequested = DateTime.Now,
            };
        }

        private ObjectResult KeywordProblem(int status, string keyword, string? detail)
        {
            ObjectResult result = Problem(
                statusCode: status,
                title: "The file was not accepted.",
                detail: detail);
            ((ProblemDetails)result.Value!).Extensions["keyword"] = keyword;
            return result;
        }
    }
}
