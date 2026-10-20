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
    using Myss.FormSpec;

    /// <summary>
    /// The admin form editor's endpoints, for IDIR staff only: list the forms, open
    /// and save a draft, and publish it as the next version. The routes match the
    /// ones MyssApi served before the split, so clients only change their base URL.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/forms")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.AdminIdir)]
    public class FormsController : Controller
    {
        private readonly ILogger<FormsController> _logger;

        private readonly IFormSpecAdminService _formsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormsController"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="formsService">Injected form spec admin service.</param>
        public FormsController(ILogger<FormsController> logger, IFormSpecAdminService formsService)
        {
            _logger = logger;
            _formsService = formsService;
        }

        /// <summary>
        /// Lists every form and its versions (admin form editor).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("ListForms")]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<FormSummaryModel>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<IReadOnlyList<FormSummaryModel>>>> ListForms(
            CancellationToken cancellationToken
        )
        {
            IReadOnlyList<FormSummaryModel> forms;
            try
            {
                forms = await _formsService.ListFormsAsync(cancellationToken);
            }
            catch (ContentEngineUnavailableException ex)
            {
                return ContentEngineUnavailable(ex);
            }
            return new BaseResponseModel<IReadOnlyList<FormSummaryModel>>
            {
                Payload = forms,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Returns the spec to open in the editor: the in-progress draft, or the
        /// latest published version as the starting point for a new draft.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{formSpecId}/draft")]
        [Produces("application/json")]
        [EndpointName("GetFormDraft")]
        [ProducesResponseType(typeof(BaseResponseModel<FormSpecModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<FormSpecModel>>> GetDraft(
            string formSpecId,
            CancellationToken cancellationToken
        )
        {
            FormSpecModel? draft;
            try
            {
                draft = await _formsService.GetDraftOrLatestPublishedAsync(formSpecId, cancellationToken);
            }
            catch (ContentEngineUnavailableException ex)
            {
                return ContentEngineUnavailable(ex);
            }

            if (draft is null)
            {
                return NotFound();
            }

            return new BaseResponseModel<FormSpecModel>
            {
                Payload = draft,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Validates and saves an edited spec as a draft (not published).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="request">The edited spec and title.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPut("{formSpecId}/draft")]
        [Produces("application/json")]
        [EndpointName("SaveFormDraft")]
        [ProducesResponseType(typeof(BaseResponseModel<FormSpecModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<FormSpecModel>>> SaveDraft(
            string formSpecId,
            [FromBody] SaveDraftRequestModel request,
            CancellationToken cancellationToken
        )
        {
            FormSpecWriteResultModel<FormSpecModel> result;
            try
            {
                result = await _formsService.SaveDraftAsync(
                    formSpecId,
                    request.Spec,
                    request.Title,
                    cancellationToken
                );
            }
            catch (StrapiWriteException ex)
            {
                return ContentEngineUnavailable(ex);
            }
            catch (ContentEngineUnavailableException ex)
            {
                return ContentEngineUnavailable(ex);
            }

            // 422 with the full error collection
            if (!result.IsValid)
            {
                return UnprocessableEntity(
                    new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    }
                );
            }

            return new BaseResponseModel<FormSpecModel>
            {
                Payload = result.Value!,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Publishes the current draft as the next version.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{formSpecId}/publish")]
        [Produces("application/json")]
        [EndpointName("PublishForm")]
        [ProducesResponseType(typeof(BaseResponseModel<PublishResultModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<PublishResultModel>>> Publish(
            string formSpecId,
            CancellationToken cancellationToken
        )
        {
            FormSpecWriteResultModel<PublishResultModel> result;
            try
            {
                result = await _formsService.PublishAsync(
                    formSpecId,
                    cancellationToken
                );
            }
            catch (StrapiWriteException ex)
            {
                return ContentEngineUnavailable(ex);
            }
            catch (ContentEngineUnavailableException ex)
            {
                return ContentEngineUnavailable(ex);
            }

            if (!result.IsValid)
            {
                return UnprocessableEntity(
                    new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    }
                );
            }

            return new BaseResponseModel<PublishResultModel>
            {
                Payload = result.Value!,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// 502 for an upstream content-engine failure - a non-lifecycle Strapi error
        /// such as a bad admin token or Strapi being unreachable. The client message is
        /// generic; the provider has already logged the status and body, so no upstream
        /// detail is leaked to the caller.
        /// </summary>
        /// <param name="ex">The upstream failure.</param>
        /// <returns>A 502 Bad Gateway result.</returns>
        private ObjectResult ContentEngineUnavailable(Exception ex)
        {
            _logger.LogError(ex, "Content engine request failed; returning 502.");

            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Content engine unavailable",
                detail: "The request could not be completed because the content engine is unavailable. Please try again.");
        }
    }
}
