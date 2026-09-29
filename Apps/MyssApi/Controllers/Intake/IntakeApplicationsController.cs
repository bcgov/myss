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
    using Myss.Api.Configuration;
    using Myss.Api.Models;
    using Myss.Api.Platform;

    /// <summary>
    /// The applicant side of Application Intake: a citizen's own Income
    /// Assistance applications. Every route is scoped to the caller; an
    /// application another citizen owns is a 404 here, never a 403, so ids
    /// cannot be enumerated. Workers do not use these routes: the review
    /// surface is follow-up work with its own controller.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/intake/applications")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.Client)]
    public class IntakeApplicationsController : Controller
    {
        private readonly ILogger<IntakeApplicationsController> _logger;
        private readonly IIntakeService _intakeService;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntakeApplicationsController"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="intakeService">Injected intake service.</param>
        public IntakeApplicationsController(
            ILogger<IntakeApplicationsController> logger,
            IIntakeService intakeService)
        {
            _logger = logger;
            _intakeService = intakeService;
        }

        /// <summary>
        /// Starts a new draft application pinned to the latest published spec version.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost]
        [Produces("application/json")]
        [EndpointName("CreateApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<ApplicationModel>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<ApplicationModel>>> Create(CancellationToken cancellationToken)
        {
            IntakeResultModel result = await _intakeService.CreateAsync(cancellationToken);
            if (result.Outcome != IntakeOutcome.Ok)
            {
                return Map(result);
            }

            return CreatedAtAction(
                nameof(Get),
                new { version = "1", id = result.Application!.Id },
                Envelope(result.Application));
        }

        /// <summary>
        /// Lists the caller's applications, newest first.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("ListApplications")]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ApplicationSummaryModel>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<BaseResponseModel<IReadOnlyList<ApplicationSummaryModel>>>> List(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ApplicationSummaryModel> applications = await _intakeService.ListMineAsync(cancellationToken);
            return new BaseResponseModel<IReadOnlyList<ApplicationSummaryModel>>
            {
                Payload = applications,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Returns one of the caller's applications with the archived spec that renders it.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{id:guid}")]
        [Produces("application/json")]
        [EndpointName("GetApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<ApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BaseResponseModel<ApplicationModel>>> Get(Guid id, CancellationToken cancellationToken)
        {
            return Map(await _intakeService.GetAsync(id, cancellationToken));
        }

        /// <summary>
        /// Saves the draft's answers. A draft may be incomplete; only submit
        /// enforces required fields.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The row version last seen and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPut("{id:guid}/answers")]
        [Produces("application/json")]
        [EndpointName("SaveApplicationAnswers")]
        [ProducesResponseType(typeof(BaseResponseModel<ApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<ApplicationModel>>> SaveAnswers(
            Guid id,
            [FromBody] ApplicationAnswersRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _intakeService.SaveAnswersAsync(id, request, cancellationToken));
        }

        /// <summary>
        /// Submits the draft. After this the application is read-only.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The row version last seen and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/submit")]
        [Produces("application/json")]
        [EndpointName("SubmitApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<ApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<ApplicationModel>>> Submit(
            Guid id,
            [FromBody] ApplicationAnswersRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _intakeService.SubmitAsync(id, request, cancellationToken));
        }

        private static BaseResponseModel<ApplicationModel> Envelope(ApplicationModel application)
        {
            return new BaseResponseModel<ApplicationModel>
            {
                Payload = application,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// One mapping from service outcome to HTTP. 422 keeps the forms shape
        /// (every error at once, for the error summary); everything else that
        /// is not a success is a ProblemDetails with a stable keyword.
        /// </summary>
        private ActionResult<BaseResponseModel<ApplicationModel>> Map(IntakeResultModel result)
        {
            switch (result.Outcome)
            {
                case IntakeOutcome.Ok:
                    return Envelope(result.Application!);

                case IntakeOutcome.NotFound:
                    return NotFound();

                case IntakeOutcome.Invalid:
                    return UnprocessableEntity(new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    });

                case IntakeOutcome.ProfileRequired:
                    return Keyworded(
                        StatusCodes.Status403Forbidden,
                        IntakeKeywords.ProfileRequired,
                        "A registered profile is required.",
                        "Register before starting an application.",
                        currentVersion: null);

                case IntakeOutcome.NotEditable:
                    return Keyworded(
                        StatusCodes.Status409Conflict,
                        IntakeKeywords.NotEditable,
                        "This application can no longer be changed.",
                        "It has been submitted.",
                        result.CurrentVersion);

                case IntakeOutcome.StaleVersion:
                    return Keyworded(
                        StatusCodes.Status409Conflict,
                        IntakeKeywords.Conflict,
                        "This application was changed elsewhere.",
                        "Another tab or window saved a newer version. Reload to see it.",
                        result.CurrentVersion);

                case IntakeOutcome.StreamConflict:
                    return Keyworded(
                        StatusCodes.Status409Conflict,
                        PlatformKeywords.EventStoreConflict,
                        "This application was changed elsewhere.",
                        "It was submitted from another tab or window. Reload to see it.",
                        result.CurrentVersion);

                case IntakeOutcome.SpecUnavailable:
                    _logger.LogError("Content engine could not supply the spec; returning 502.");
                    return Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Content engine unavailable",
                        detail: "The request could not be completed because the content engine is unavailable. Please try again.");

                default:
                    throw new InvalidOperationException($"Unmapped intake outcome {result.Outcome}.");
            }
        }

        private ObjectResult Keyworded(int statusCode, string keyword, string title, string detail, int? currentVersion)
        {
            ObjectResult problem = Problem(statusCode: statusCode, title: title, detail: detail);
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
