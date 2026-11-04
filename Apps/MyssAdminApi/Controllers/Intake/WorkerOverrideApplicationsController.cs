namespace Myss.AdminApi.Intake
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
    using Myss.Common.Models;
    using Myss.Data.Intake;
    using Myss.FormSpec;

    /// <summary>
    /// Worker override (MYSS-321): a worker submits the simplified Application
    /// for Assistance for a client who cannot submit their own. Start a draft,
    /// open it, save it, submit it. Any worker may open any draft; the submit
    /// makes the client's worker override account and puts the application in
    /// the worker queue. Staff only, with an IDIR identity.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/intake/worker-override/applications")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.WorkerWithIdir)]
    public class WorkerOverrideApplicationsController : ControllerBase
    {
        private readonly IWorkerOverrideService _service;
        private readonly ILogger<WorkerOverrideApplicationsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerOverrideApplicationsController"/> class.
        /// </summary>
        /// <param name="service">Injected worker override service.</param>
        /// <param name="logger">Injected logger.</param>
        public WorkerOverrideApplicationsController(
            IWorkerOverrideService service,
            ILogger<WorkerOverrideApplicationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Starts a draft on the latest published worker override form.</summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost]
        [Produces("application/json")]
        [EndpointName("CreateWorkerOverrideApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<WorkerOverrideApplicationModel>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BaseResponseModel<WorkerOverrideApplicationModel>>> Create(CancellationToken cancellationToken)
        {
            WorkerOverrideResultModel result = await _service.CreateAsync(cancellationToken);
            if (result.Outcome != WorkerOverrideOutcome.Ok)
            {
                return Map(result);
            }

            return CreatedAtAction(
                nameof(Get),
                new { version = "1", id = result.Application!.Id },
                Envelope(result.Application));
        }

        /// <summary>Returns a worker override application with the spec that renders it.</summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{id:guid}")]
        [Produces("application/json")]
        [EndpointName("GetWorkerOverrideApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<WorkerOverrideApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BaseResponseModel<WorkerOverrideApplicationModel>>> Get(
            Guid id,
            CancellationToken cancellationToken)
        {
            return Map(await _service.GetAsync(id, cancellationToken));
        }

        /// <summary>Saves a draft's answers, complete or not.</summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The row version last read and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPut("{id:guid}/answers")]
        [Produces("application/json")]
        [EndpointName("SaveWorkerOverrideAnswers")]
        [ProducesResponseType(typeof(BaseResponseModel<WorkerOverrideApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<WorkerOverrideApplicationModel>>> SaveAnswers(
            Guid id,
            [FromBody] WorkerOverrideAnswersRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _service.SaveAnswersAsync(id, request, cancellationToken));
        }

        /// <summary>
        /// Submits: makes the client's worker override account and puts the
        /// application in the worker queue.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The row version last read and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/submit")]
        [Produces("application/json")]
        [EndpointName("SubmitWorkerOverrideApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<WorkerOverrideApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<WorkerOverrideApplicationModel>>> Submit(
            Guid id,
            [FromBody] WorkerOverrideAnswersRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _service.SubmitAsync(id, request, cancellationToken));
        }

        private static BaseResponseModel<WorkerOverrideApplicationModel> Envelope(WorkerOverrideApplicationModel application) =>
            new() { Payload = application, DatetimeRequested = DateTime.Now };

        private ActionResult<BaseResponseModel<WorkerOverrideApplicationModel>> Map(WorkerOverrideResultModel result)
        {
            switch (result.Outcome)
            {
                case WorkerOverrideOutcome.Ok:
                    return Envelope(result.Application!);

                case WorkerOverrideOutcome.NotFound:
                    return NotFound();

                case WorkerOverrideOutcome.Invalid:
                    return UnprocessableEntity(new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    });

                case WorkerOverrideOutcome.NotEditable:
                    return Keyworded(
                        StatusCodes.Status409Conflict,
                        IntakeKeywords.NotEditable,
                        "This application can no longer be changed.",
                        "It has been submitted and has not been returned for more information.",
                        result.CurrentVersion);

                case WorkerOverrideOutcome.StaleVersion:
                    return Keyworded(
                        StatusCodes.Status409Conflict,
                        IntakeKeywords.Conflict,
                        "This application was changed elsewhere.",
                        "Another worker, tab or window saved a newer version. Reload to see it.",
                        result.CurrentVersion);

                case WorkerOverrideOutcome.SpecUnavailable:
                    _logger.LogError("Content engine could not supply the worker override spec; returning 502.");
                    return Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Content engine unavailable",
                        detail: "The request could not be completed because the content engine is unavailable. Please try again.");

                case WorkerOverrideOutcome.SpecNotPublished:
                    return Keyworded(
                        StatusCodes.Status404NotFound,
                        IntakeKeywords.SpecNotPublished,
                        "Worker override is not available yet.",
                        "The worker override form must be published before starting an application.",
                        currentVersion: null);

                default:
                    throw new InvalidOperationException($"Unmapped worker override outcome {result.Outcome}.");
            }
        }

        private ObjectResult Keyworded(int status, string keyword, string title, string detail, int? currentVersion)
        {
            ObjectResult problem = Problem(statusCode: status, title: title, detail: detail);
            var details = (ProblemDetails)problem.Value!;
            details.Extensions["keyword"] = keyword;
            if (currentVersion is not null)
            {
                details.Extensions["currentVersion"] = currentVersion;
            }

            return problem;
        }
    }
}
