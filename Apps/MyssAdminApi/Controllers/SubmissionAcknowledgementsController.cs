namespace Myss.AdminApi.Controllers
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
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Services;
    using Myss.Common.Content;
    using Myss.Common.Models;
    using Myss.FormSpec;

    /// <summary>
    /// The confirmation message editor's endpoints, for IDIR staff only: read a form's
    /// live message and its history, and save a new one.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/forms/{formSpecId}/acknowledgement")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.AdminIdir)]
    public class SubmissionAcknowledgementsController : ControllerBase
    {
        private readonly ILogger<SubmissionAcknowledgementsController> _logger;
        private readonly ISubmissionAcknowledgementService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubmissionAcknowledgementsController"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="service">Injected confirmation message service.</param>
        public SubmissionAcknowledgementsController(
            ILogger<SubmissionAcknowledgementsController> logger,
            ISubmissionAcknowledgementService service)
        {
            _logger = logger;
            _service = service;
        }

        /// <summary>
        /// Returns a form's live confirmation message and its history, read fresh. A 404
        /// for a form whose page shows no confirmation message; a 502 when the content
        /// engine cannot be read.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("GetSubmissionAcknowledgement")]
        [ProducesResponseType(typeof(BaseResponseModel<SubmissionAcknowledgementAdminModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<SubmissionAcknowledgementAdminModel>>> Get(
            string formSpecId,
            CancellationToken cancellationToken)
        {
            if (!SubmissionAcknowledgementForms.Ids.Contains(formSpecId))
            {
                return NotFound();
            }

            SubmissionAcknowledgementAdminModel acknowledgement;
            try
            {
                acknowledgement = await _service.GetAsync(formSpecId, cancellationToken);
            }
            catch (ContentEngineUnavailableException ex)
            {
                _logger.LogError(ex, "Reading the confirmation message failed in the content engine; returning 502.");
                return ContentEngineUnavailable("loaded");
            }

            return Payload(acknowledgement);
        }

        /// <summary>
        /// Saves and publishes a form's new confirmation message. A 422 with every
        /// refused rule; a 409 when the live message changed after the edit began.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="request">The texts, and the entry the edit started from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost]
        // A message at its limits is under 8 KB of JSON; nothing larger reaches the rules.
        [RequestSizeLimit(16 * 1024)]
        [Produces("application/json")]
        [EndpointName("SaveSubmissionAcknowledgement")]
        [ProducesResponseType(typeof(BaseResponseModel<SubmissionAcknowledgementAdminModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<SubmissionAcknowledgementAdminModel>>> Save(
            string formSpecId,
            [FromBody] SaveSubmissionAcknowledgementRequestModel request,
            CancellationToken cancellationToken)
        {
            if (!SubmissionAcknowledgementForms.Ids.Contains(formSpecId))
            {
                return NotFound();
            }

            SubmissionAcknowledgementSaveResultModel result;
            try
            {
                result = await _service.SaveAsync(formSpecId, request, cancellationToken);
            }
            catch (Exception ex) when (ex is StrapiWriteException or ContentEngineUnavailableException)
            {
                _logger.LogError(ex, "Saving the confirmation message failed in the content engine; returning 502.");
                return ContentEngineUnavailable("saved");
            }

            if (result.IsChanged)
            {
                return Conflict(Errors(result.Errors));
            }

            if (!result.IsValid)
            {
                return UnprocessableEntity(Errors(result.Errors));
            }

            return Payload(result.Saved);
        }

        private static BaseResponseModel<SubmissionAcknowledgementAdminModel> Payload(
            SubmissionAcknowledgementAdminModel acknowledgement) =>
            new() { Payload = acknowledgement, DatetimeRequested = DateTime.Now };

        private static BaseResponseModel<IReadOnlyList<ValidationErrorModel>> Errors(
            IReadOnlyList<ValidationErrorModel> errors) =>
            new() { Payload = errors, DatetimeRequested = DateTime.Now };

        private ObjectResult ContentEngineUnavailable(string action) =>
            Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Content engine unavailable",
                detail: $"The confirmation message could not be {action} because the content engine is unavailable. Please try again.");
    }
}
