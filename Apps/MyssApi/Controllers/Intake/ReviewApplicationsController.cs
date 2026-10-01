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
    /// submitted applications, one application read-only, and the three
    /// status moves. Staff only, with an IDIR identity; any worker may act on
    /// any file, and the event records who did.
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
        /// Marks the application Under Review.
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
