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
    /// The form message editor's endpoints, for IDIR staff only: list the defined
    /// messages, read one message's live texts and history, and save a new version.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.AdminIdir)]
    public class FormMessagesController : ControllerBase
    {
        private readonly ILogger<FormMessagesController> _logger;
        private readonly IFormMessageService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormMessagesController"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="service">Injected form message service.</param>
        public FormMessagesController(
            ILogger<FormMessagesController> logger,
            IFormMessageService service)
        {
            _logger = logger;
            _service = service;
        }

        /// <summary>
        /// Lists every defined message with its last change, read fresh. A 502 when the
        /// content engine cannot be read.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("form-messages")]
        [Produces("application/json")]
        [EndpointName("ListFormMessages")]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<FormMessageSummaryModel>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<IReadOnlyList<FormMessageSummaryModel>>>> List(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<FormMessageSummaryModel> summaries;
            try
            {
                summaries = await _service.ListAsync(cancellationToken);
            }
            catch (ContentEngineUnavailableException ex)
            {
                _logger.LogError(ex, "Listing the form messages failed in the content engine; returning 502.");
                return ContentEngineUnavailable("The form messages could not be loaded");
            }

            return new BaseResponseModel<IReadOnlyList<FormMessageSummaryModel>>
            {
                Payload = summaries,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Returns a form message's definition, live texts and history, read fresh. A 404
        /// when the message is not defined; a 502 when the content engine cannot be read.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="messageKey">The message's key within the form.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("forms/{formSpecId}/messages/{messageKey}")]
        [Produces("application/json")]
        [EndpointName("GetFormMessage")]
        [ProducesResponseType(typeof(BaseResponseModel<FormMessageAdminModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<FormMessageAdminModel>>> Get(
            string formSpecId,
            string messageKey,
            CancellationToken cancellationToken)
        {
            if (FormMessageDefinitions.Find(formSpecId, messageKey) is not FormMessageDefinition definition)
            {
                return NotFound();
            }

            FormMessageAdminModel message;
            try
            {
                message = await _service.GetAsync(definition, cancellationToken);
            }
            catch (ContentEngineUnavailableException ex)
            {
                _logger.LogError(ex, "Reading the form message failed in the content engine; returning 502.");
                return ContentEngineUnavailable("The form message could not be loaded");
            }

            return Payload(message);
        }

        /// <summary>
        /// Saves and publishes a form message's new texts. A 404 when the message is not
        /// defined; a 422 with every refused rule; a 409 when the live message changed
        /// after the edit began.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="messageKey">The message's key within the form.</param>
        /// <param name="request">The texts, and the entry the edit started from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("forms/{formSpecId}/messages/{messageKey}")]
        // A message at its limits is under 8 KB of JSON; nothing larger reaches the rules.
        [RequestSizeLimit(16 * 1024)]
        [Produces("application/json")]
        [EndpointName("SaveFormMessage")]
        [ProducesResponseType(typeof(BaseResponseModel<FormMessageAdminModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<FormMessageAdminModel>>> Save(
            string formSpecId,
            string messageKey,
            [FromBody] SaveFormMessageRequestModel request,
            CancellationToken cancellationToken)
        {
            if (FormMessageDefinitions.Find(formSpecId, messageKey) is not FormMessageDefinition definition)
            {
                return NotFound();
            }

            FormMessageSaveResultModel result;
            try
            {
                result = await _service.SaveAsync(definition, request, cancellationToken);
            }
            catch (Exception ex) when (ex is StrapiWriteException or ContentEngineUnavailableException)
            {
                _logger.LogError(ex, "Saving the form message failed in the content engine; returning 502.");
                return ContentEngineUnavailable("The form message could not be saved");
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

        private static BaseResponseModel<FormMessageAdminModel> Payload(
            FormMessageAdminModel message) =>
            new() { Payload = message, DatetimeRequested = DateTime.Now };

        private static BaseResponseModel<IReadOnlyList<ValidationErrorModel>> Errors(
            IReadOnlyList<ValidationErrorModel> errors) =>
            new() { Payload = errors, DatetimeRequested = DateTime.Now };

        private ObjectResult ContentEngineUnavailable(string outcome) =>
            Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Content engine unavailable",
                detail: $"{outcome} because the content engine is unavailable. Please try again.");
    }
}
