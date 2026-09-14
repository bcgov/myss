namespace Myss.Api.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
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
    /// BC Bus Pass submission endpoints.
    /// Public: like <see cref="EligibilityEstimatorController"/>, this does not require the
    /// caller to be signed in, matching the legacy form the program linked to directly.
    /// A submission is validated, stored, then handed to ICM through the middleware;
    /// the response says which of those happened. Non-accepted outcomes carry a
    /// stable dotted keyword (e.g. <c>BUSPASS.SUBMIT.REJECTED</c>) for the
    /// frontend to match on.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/bus-pass")]
    [ApiController]
    [AllowAnonymous]
    public class BusPassController : Controller
    {
        private readonly ILogger<BusPassController> _logger;
        private readonly IFormsService _formsService;
        private readonly IBusPassSubmissionService _submissionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassController"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="formsService">Injected Forms Service.</param>
        /// <param name="submissionService">Injected bus pass submission service.</param>
        public BusPassController(
            ILogger<BusPassController> logger,
            IFormsService formsService,
            IBusPassSubmissionService submissionService)
        {
            _logger = logger;
            _formsService = formsService;
            _submissionService = submissionService;
        }

        /// <summary>
        /// Validates and stores a BC Bus Pass submission, then hands it to ICM
        /// through the middleware. 200 with the outcome (accepted, or rejected by
        /// ICM with a keyword); 422 when validation refused it and nothing was
        /// stored; 503 when it was stored but could not be delivered, with the
        /// submission id in the problem body so nothing is lost.
        /// </summary>
        /// <param name="request">The submission payload: the spec version rendered and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("submissions")]
        [Produces("application/json")]
        [EndpointName("SubmitBusPass")]
        [ProducesResponseType(typeof(BaseResponseModel<BusPassSubmissionResponseModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BaseResponseModel<BusPassSubmissionResponseModel>>> Submit(
            [FromBody] FormSubmissionRequestModel request,
            CancellationToken cancellationToken)
        {
            BusPassSubmissionResultModel result = await _submissionService.SubmitAsync(request, cancellationToken);

            if (!result.IsValid)
            {
                return UnprocessableEntity(
                    new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    });
            }

            BusPassSubmissionResponseModel response = result.Response!;
            if (response.Outcome == BusPassSubmissionOutcome.Failed)
            {
                ObjectResult problem = Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The request was saved but could not be sent to the ministry.",
                    detail: "Try again later. Quote the submission id if you contact the ministry.");
                var details = (ProblemDetails)problem.Value!;
                details.Extensions["keyword"] = response.Keyword;
                details.Extensions["submissionId"] = response.SubmissionId;
                return problem;
            }

            return new BaseResponseModel<BusPassSubmissionResponseModel>
            {
                Payload = response,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Generates the BC Bus Pass request PDF for a stored submission.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("submissions/{id:guid}/pdf")]
        [EndpointName("GetBusPassSubmissionPdf")]
        [Produces("application/pdf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSubmissionPdf(Guid id, CancellationToken cancellationToken)
        {
            byte[]? pdf = await _formsService.GetBusPassSubmissionPdfAsync(id, cancellationToken);
            if (pdf is null)
            {
                return NotFound();
            }

            _logger.LogInformation(
                "Served bus pass PDF for submission {SubmissionId} ({Bytes} bytes)",
                id,
                pdf.Length);

            return File(pdf, "application/pdf", $"bus-pass-{id:N}.pdf");
        }
    }
}
