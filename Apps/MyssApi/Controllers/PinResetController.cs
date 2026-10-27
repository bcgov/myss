namespace Myss.Api.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.FeatureManagement.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Myss.Api.Configuration;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Myss.Common.Models;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Resetting a forgotten PIN by email (MYSS-36). Asking for the link needs the
    /// citizen's own sign-in; using it does not, because the link is the credential
    /// and the citizen signs in afterwards with the new PIN in hand.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/pin-reset")]
    [ApiController]
    [FeatureGate(FeatureFlags.PinReset)]
    public class PinResetController : ControllerBase
    {
        /// <summary>The emailed link is unknown, already used, or expired.</summary>
        public const string LinkInvalidKeyword = "ACCOUNT.PIN_RESET.LINK_INVALID";

        /// <summary>The reset email could not be sent.</summary>
        public const string EmailNotSentKeyword = "ACCOUNT.PIN_RESET.EMAIL_NOT_SENT";

        /// <summary>A reset email was sent too recently to send another.</summary>
        public const string TooSoonKeyword = "ACCOUNT.PIN_RESET.TOO_SOON";

        private readonly IPinResetService _pinResetService;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PinResetController"/> class.
        /// </summary>
        /// <param name="pinResetService">Injected PIN reset service.</param>
        /// <param name="timeProvider">The clock the request cooldown is measured by.</param>
        public PinResetController(IPinResetService pinResetService, TimeProvider timeProvider)
        {
            _pinResetService = pinResetService;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Emails the caller a single-use link to reset their PIN, at the address
        /// they registered with. Basic BCeID only. A new request replaces any
        /// earlier link. One email per cooldown
        /// (<c>Account:PinReset:RequestCooldownSeconds</c>); a request inside it
        /// is a 429 with Retry-After.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("requests")]
        [Authorize(Policy = MyssPolicies.Client)]
        [Produces("application/json")]
        [EndpointName("RequestPinReset")]
        [ProducesResponseType(typeof(BaseResponseModel<PinResetRequestedModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BaseResponseModel<PinResetRequestedModel>>> RequestReset(CancellationToken cancellationToken)
        {
            PinResetResultModel result = await _pinResetService.RequestAsync(cancellationToken);
            if (result.Outcome == PinResetOutcome.Ok)
            {
                return new BaseResponseModel<PinResetRequestedModel>
                {
                    Payload = new PinResetRequestedModel { ExpiresAt = result.ExpiresAt!.Value },
                    DatetimeRequested = DateTime.Now,
                };
            }

            return Refusal(result);
        }

        /// <summary>
        /// Sets a new PIN from an emailed link and uses the link up. Anonymous:
        /// the token in the body is the credential. The PIN is never returned or
        /// logged, and is not sent to ICM (RULE-IDA-04).
        /// </summary>
        /// <param name="request">The token from the link and the new PIN twice.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost]
        [AllowAnonymous]
        [Produces("application/json")]
        [EndpointName("ResetPin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Reset(
            [FromBody] ResetPinRequestModel request,
            CancellationToken cancellationToken)
        {
            PinResetResultModel result = await _pinResetService.ResetAsync(request, cancellationToken);
            if (result.Outcome == PinResetOutcome.Ok)
            {
                return NoContent();
            }

            return Refusal(result);
        }

        /// <summary>
        /// The refusals, in the account controller's shapes: 422 carries every
        /// field error at once, anything else is a ProblemDetails with a stable keyword.
        /// </summary>
        private ActionResult Refusal(PinResetResultModel result)
        {
            switch (result.Outcome)
            {
                case PinResetOutcome.Invalid:
                    return UnprocessableEntity(new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    });

                case PinResetOutcome.ProfileRequired:
                    return Keyed(Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "A registered profile is required.",
                        detail: "Register before resetting your PIN."), AccountController.ProfileRequiredKeyword);

                case PinResetOutcome.PinNotAvailable:
                    return Keyed(Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "This sign-in does not use a PIN.",
                        detail: "A PIN is only used when you sign in with Basic BCeID."), ValidationKeywords.PinNotAvailable);

                case PinResetOutcome.EmailNotSent:
                    return Keyed(Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "The reset email could not be sent.",
                        detail: "Try again in a few minutes."), EmailNotSentKeyword);

                case PinResetOutcome.TooSoon:
                    return TooSoon(result.RetryAt!.Value);

                case PinResetOutcome.LinkInvalid:
                    return Keyed(Problem(
                        statusCode: StatusCodes.Status410Gone,
                        title: "This PIN reset link no longer works.",
                        detail: "The link has expired or has already been used. "
                            + "Sign in and choose Reset PIN on Account Info to get a new one."), LinkInvalidKeyword);

                default:
                    throw new InvalidOperationException($"Unmapped PIN reset outcome {result.Outcome}.");
            }
        }

        /// <summary>
        /// 429 in the shape the PIN lockout uses: a Retry-After header and a
        /// ProblemDetails with a stable keyword and the time to retry at.
        /// </summary>
        private ObjectResult TooSoon(DateTimeOffset retryAt)
        {
            int seconds = Math.Max(1, (int)Math.Ceiling((retryAt - _timeProvider.GetUtcNow()).TotalSeconds));
            Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            int minutes = (int)Math.Ceiling(seconds / 60.0);
            string wait = seconds < 60
                ? $"{seconds} second{(seconds == 1 ? string.Empty : "s")}"
                : $"{minutes} minute{(minutes == 1 ? string.Empty : "s")}";

            ObjectResult tooSoon = Keyed(
                Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "A reset email was sent recently.",
                    detail: $"We just sent you a reset email. Check your inbox, or try again in {wait}."),
                TooSoonKeyword);
            ((ProblemDetails)tooSoon.Value!).Extensions["retryAt"] = retryAt;
            return tooSoon;
        }

        private static ObjectResult Keyed(ObjectResult problem, string keyword)
        {
            ((ProblemDetails)problem.Value!).Extensions["keyword"] = keyword;
            return problem;
        }
    }
}
