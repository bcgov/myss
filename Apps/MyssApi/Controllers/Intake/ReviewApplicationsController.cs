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
    using Myss.Api.Configuration;
    using Myss.Api.Models;
    using Myss.Api.Platform;

    /// <summary>
    /// The worker side of Application Intake for the POC: the list of
    /// submitted applications, one application read-only, the review moves
    /// and internal notes. Staff only, with an IDIR identity. Any worker may
    /// pick up a file from the queue; while it is Under Review only that
    /// worker may act on it, and every event records who did.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/intake/review/applications")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.WorkerWithIdir)]
    public class ReviewApplicationsController : Controller
    {
        private readonly IReviewService _reviewService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewApplicationsController"/> class.
        /// </summary>
        /// <param name="reviewService">Injected review service.</param>
        public ReviewApplicationsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>
        /// Lists every submitted application, newest submission first.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("ListReviewApplications")]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ReviewApplicationSummaryModel>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<BaseResponseModel<IReadOnlyList<ReviewApplicationSummaryModel>>>> List(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ReviewApplicationSummaryModel> applications = await _reviewService.ListAsync(cancellationToken);
            return new BaseResponseModel<IReadOnlyList<ReviewApplicationSummaryModel>>
            {
                Payload = applications,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Returns a submitted application with the archived spec that renders it
        /// and the actions available right now.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{id:guid}")]
        [Produces("application/json")]
        [EndpointName("GetReviewApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<ReviewApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BaseResponseModel<ReviewApplicationModel>>> Get(Guid id, CancellationToken cancellationToken)
        {
            return Map(await _reviewService.GetAsync(id, cancellationToken));
        }

        /// <summary>
        /// Picks the application up from the queue: it goes Under Review,
        /// assigned to the caller.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The stream version last seen.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/review")]
        [Produces("application/json")]
        [EndpointName("StartApplicationReview")]
        [ProducesResponseType(typeof(BaseResponseModel<ReviewApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BaseResponseModel<ReviewApplicationModel>>> Review(
            Guid id,
            [FromBody] ReviewActionRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _reviewService.ActAsync(id, WorkerActions.Review, request, cancellationToken));
        }

        /// <summary>
        /// Accepts the application.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The stream version last seen.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/accept")]
        [Produces("application/json")]
        [EndpointName("AcceptApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<ReviewApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BaseResponseModel<ReviewApplicationModel>>> Accept(
            Guid id,
            [FromBody] ReviewActionRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _reviewService.ActAsync(id, WorkerActions.Accept, request, cancellationToken));
        }

        /// <summary>
        /// Denies the application.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The stream version last seen.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/deny")]
        [Produces("application/json")]
        [EndpointName("DenyApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<ReviewApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BaseResponseModel<ReviewApplicationModel>>> Deny(
            Guid id,
            [FromBody] ReviewActionRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _reviewService.ActAsync(id, WorkerActions.Deny, request, cancellationToken));
        }

        /// <summary>
        /// Puts the application back in the queue without deciding it. Assignee only.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The stream version last seen.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/release")]
        [Produces("application/json")]
        [EndpointName("ReleaseApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<ReviewApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BaseResponseModel<ReviewApplicationModel>>> Release(
            Guid id,
            [FromBody] ReviewActionRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _reviewService.ActAsync(id, WorkerActions.Release, request, cancellationToken));
        }

        /// <summary>
        /// Returns the application to the applicant for more information. Assignee only.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The stream version last seen.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/request-more-info")]
        [Produces("application/json")]
        [EndpointName("RequestMoreApplicationInfo")]
        [ProducesResponseType(typeof(BaseResponseModel<ReviewApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BaseResponseModel<ReviewApplicationModel>>> RequestMoreInfo(
            Guid id,
            [FromBody] ReviewActionRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _reviewService.ActAsync(id, WorkerActions.RequestMoreInfo, request, cancellationToken));
        }

        /// <summary>
        /// Escalates the application for a further review. Assignee only.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The stream version last seen.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/escalate")]
        [Produces("application/json")]
        [EndpointName("EscalateApplication")]
        [ProducesResponseType(typeof(BaseResponseModel<ReviewApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BaseResponseModel<ReviewApplicationModel>>> Escalate(
            Guid id,
            [FromBody] ReviewActionRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _reviewService.ActAsync(id, WorkerActions.Escalate, request, cancellationToken));
        }

        /// <summary>
        /// Adds an internal note. Assignee only, while the application is Under Review.
        /// Notes are never shown to the applicant.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The stream version last seen and the note.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("{id:guid}/notes")]
        [Produces("application/json")]
        [EndpointName("AddApplicationNote")]
        [ProducesResponseType(typeof(BaseResponseModel<ReviewApplicationModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BaseResponseModel<ReviewApplicationModel>>> AddNote(
            Guid id,
            [FromBody] ReviewNoteRequestModel request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                ModelState.AddModelError(nameof(request.Text), "A note needs some text.");
                return ValidationProblem(ModelState);
            }

            return Map(await _reviewService.AddNoteAsync(id, request, cancellationToken));
        }

        private ActionResult<BaseResponseModel<ReviewApplicationModel>> Map(ReviewResultModel result)
        {
            switch (result.Outcome)
            {
                case ReviewOutcome.Ok:
                    return new BaseResponseModel<ReviewApplicationModel>
                    {
                        Payload = result.Application!,
                        DatetimeRequested = DateTime.Now,
                    };

                case ReviewOutcome.NotFound:
                    return NotFound();

                case ReviewOutcome.NotAllowed:
                    return Keyworded(
                        IntakeKeywords.ReviewNotAllowed,
                        "This action is not available for the application right now.",
                        "Its status has moved on. Reload to see the actions that apply.",
                        result.CurrentVersion);

                case ReviewOutcome.AssignedToAnother:
                    return Keyworded(
                        IntakeKeywords.AssignedToAnother,
                        "This application is assigned to another worker.",
                        $"It is under review by {result.Assignee}. Only they can act on it until they decide it or release it.",
                        result.CurrentVersion);

                case ReviewOutcome.StaleVersion:
                    return Keyworded(
                        PlatformKeywords.EventStoreConflict,
                        "This application was changed by another worker.",
                        "Reload to see its current status before acting.",
                        result.CurrentVersion);

                default:
                    throw new InvalidOperationException($"Unmapped review outcome {result.Outcome}.");
            }
        }

        private ObjectResult Keyworded(string keyword, string title, string detail, int? currentVersion)
        {
            ObjectResult problem = Problem(statusCode: StatusCodes.Status409Conflict, title: title, detail: detail);
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
