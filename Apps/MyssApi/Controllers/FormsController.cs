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
    using Myss.Api.Services;

    /// <summary>
    /// The forms controller: versioned specs and submissions.
    /// Protected: forms are only available after the user has authenticated.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/forms")]
    [ApiController]
    [Authorize]
    public class FormsController : Controller
    {
        private readonly ILogger<FormsController> _logger;

        private readonly IFormsService _formsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormsController"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="formsService">Injected Forms Service.</param>
        public FormsController(ILogger<FormsController> logger, IFormsService formsService)
        {
            _logger = logger;
            _formsService = formsService;
        }

        /// <summary>
        /// Returns the latest published spec for a form (content-engine proxy).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{formSpecId}/spec")]
        [Produces("application/json")]
        [EndpointName("GetFormSpec")]
        [ProducesResponseType(typeof(BaseResponseModel<FormSpecModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BaseResponseModel<FormSpecModel>>> GetSpec(
            string formSpecId,
            CancellationToken cancellationToken
        )
        {
            FormSpecModel? spec = await _formsService.GetLatestSpecAsync(
                formSpecId,
                cancellationToken
            );
            if (spec is null)
            {
                _logger.LogDebug("No published spec found for {FormSpecId}", formSpecId);
                return NotFound();
            }

            return new BaseResponseModel<FormSpecModel>
            {
                Payload = spec,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Stores a submission stamped with the spec version it was rendered with.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="request">The submission payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{formSpecId}/submissions")]
        [Produces("application/json")]
        [EndpointName("SubmitForm")]
        [ProducesResponseType(
            typeof(BaseResponseModel<FormSubmissionResponseModel>),
            StatusCodes.Status200OK
        )]
        [ProducesResponseType(
            typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>),
            StatusCodes.Status422UnprocessableEntity
        )]
        public async Task<ActionResult<BaseResponseModel<FormSubmissionResponseModel>>> Submit(
            string formSpecId,
            [FromBody] FormSubmissionRequestModel request,
            CancellationToken cancellationToken
        )
        {
            FormSubmissionResultModel result = await _formsService.SubmitAsync(
                formSpecId,
                request,
                cancellationToken
            );

            // 422 rather than 400: the request was well-formed JSON the server
            // understood, and was refused on its contents. The body carries the
            // full error collection so the client can build the WCAG error
            // summary in one pass instead of discovering faults one at a time.
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

            return new BaseResponseModel<FormSubmissionResponseModel>
            {
                Payload = result.Submission!,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Lists a form's submissions, newest first (metadata only).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{formSpecId}/submissions")]
        [Produces("application/json")]
        [EndpointName("ListFormSubmissions")]
        [ProducesResponseType(
            typeof(BaseResponseModel<IReadOnlyList<FormSubmissionSummaryModel>>),
            StatusCodes.Status200OK
        )]
        public async Task<
            ActionResult<BaseResponseModel<IReadOnlyList<FormSubmissionSummaryModel>>>
        > ListSubmissions(string formSpecId, CancellationToken cancellationToken)
        {
            IReadOnlyList<FormSubmissionSummaryModel> submissions =
                await _formsService.ListSubmissionsAsync(formSpecId, cancellationToken);
            return new BaseResponseModel<IReadOnlyList<FormSubmissionSummaryModel>>
            {
                Payload = submissions,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Returns a submission with the archived spec version that rendered it.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("submissions/{id:guid}")]
        [Produces("application/json")]
        [EndpointName("GetFormSubmission")]
        [ProducesResponseType(
            typeof(BaseResponseModel<FormSubmissionResponseModel>),
            StatusCodes.Status200OK
        )]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<BaseResponseModel<FormSubmissionResponseModel>>
        > GetSubmission(Guid id, CancellationToken cancellationToken)
        {
            FormSubmissionResponseModel? submission = await _formsService.GetSubmissionAsync(
                id,
                cancellationToken
            );
            if (submission is null)
            {
                return NotFound();
            }

            return new BaseResponseModel<FormSubmissionResponseModel>
            {
                Payload = submission,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
