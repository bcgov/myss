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
    using Microsoft.AspNetCore.Routing;
    using Microsoft.FeatureManagement.Mvc;
    using Myss.Api.Configuration;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Myss.Common.Models;
    using Myss.FormSpec;

    /// <summary>
    /// Changing and proving the citizen's email address (MYSS-305). The changes
    /// are the caller's own, on Account Info; following the emailed link is
    /// anonymous, because the link is the credential and may be opened on any
    /// device. Every signed-in success answers with the account as it now stands.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}")]
    [ApiController]
    [FeatureGate(FeatureFlags.EmailVerification)]
    public class EmailVerificationController : ControllerBase
    {
        /// <summary>A verification link was sent too recently to send another.</summary>
        public const string TooSoonKeyword = "ACCOUNT.EMAIL.TOO_SOON";

        /// <summary>The verification email could not be sent.</summary>
        public const string EmailNotSentKeyword = "ACCOUNT.EMAIL.NOT_SENT";

        /// <summary>A resend was asked for with no address waiting to be verified.</summary>
        public const string NothingToVerifyKeyword = "ACCOUNT.EMAIL.NOTHING_TO_VERIFY";

        /// <summary>The emailed link is unknown, already used, replaced, or expired.</summary>
        public const string LinkInvalidKeyword = "ACCOUNT.EMAIL.LINK_INVALID";

        private readonly IEmailVerificationService _emailVerificationService;
        private readonly IAccountService _accountService;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailVerificationController"/> class.
        /// </summary>
        /// <param name="emailVerificationService">Injected email verification service.</param>
        /// <param name="accountService">Injected account service, for the account after a change.</param>
        /// <param name="timeProvider">The clock the cooldown is measured by.</param>
        public EmailVerificationController(
            IEmailVerificationService emailVerificationService,
            IAccountService accountService,
            TimeProvider timeProvider)
        {
            _emailVerificationService = emailVerificationService;
            _accountService = accountService;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Changes the caller's email address. The new address waits as pending,
        /// and is emailed a link; the current one stays in use until the link is
        /// followed. Nothing is saved unless the address passes.
        /// </summary>
        /// <param name="request">The new address.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPut("account/email")]
        [Authorize(Policy = MyssPolicies.Client)]
        [Produces("application/json")]
        [EndpointName("ChangeAccountEmail")]
        [ProducesResponseType(typeof(BaseResponseModel<AccountModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BaseResponseModel<AccountModel>>> ChangeEmail(
            [FromBody] ChangeEmailRequestModel request,
            CancellationToken cancellationToken)
        {
            return await AccountAfter(
                await _emailVerificationService.RequestChangeAsync(request, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Emails a new link for the address waiting to be verified. One email
        /// per cooldown (<c>Account:EmailVerification:RequestCooldownSeconds</c>).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("account/email/verification-requests")]
        [Authorize(Policy = MyssPolicies.Client)]
        [Produces("application/json")]
        [EndpointName("ResendAccountEmailVerification")]
        [ProducesResponseType(typeof(BaseResponseModel<AccountModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BaseResponseModel<AccountModel>>> ResendVerification(CancellationToken cancellationToken)
        {
            return await AccountAfter(
                await _emailVerificationService.ResendAsync(cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Drops a pending email change and its link; the address in use stays.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpDelete("account/email/pending")]
        [Authorize(Policy = MyssPolicies.Client)]
        [Produces("application/json")]
        [EndpointName("CancelAccountEmailChange")]
        [ProducesResponseType(typeof(BaseResponseModel<AccountModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<BaseResponseModel<AccountModel>>> CancelChange(CancellationToken cancellationToken)
        {
            return await AccountAfter(
                await _emailVerificationService.CancelChangeAsync(cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Follows an emailed verification link: a pending address becomes the
        /// one in use, or the address from registration is marked verified.
        /// Anonymous: the token in the body is the credential.
        /// </summary>
        /// <param name="request">The token from the link.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("email-verification")]
        [AllowAnonymous]
        [Produces("application/json")]
        [EndpointName("VerifyEmail")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
        public async Task<IActionResult> Verify(
            [FromBody] VerifyEmailRequestModel request,
            CancellationToken cancellationToken)
        {
            EmailVerificationResultModel result = await _emailVerificationService.VerifyAsync(request, cancellationToken);
            return result.Outcome == EmailVerificationOutcome.Ok ? NoContent() : Refusal(result);
        }

        private async Task<ActionResult<BaseResponseModel<AccountModel>>> AccountAfter(
            EmailVerificationResultModel result,
            CancellationToken cancellationToken)
        {
            if (result.Outcome != EmailVerificationOutcome.Ok)
            {
                return Refusal(result);
            }

            AccountResultModel account = await _accountService.GetAsync(cancellationToken);
            return new BaseResponseModel<AccountModel>
            {
                Payload = account.Account!,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// The refusals, in the account and PIN reset controllers' shapes: 422
        /// carries the field error, anything else is a ProblemDetails with a
        /// stable keyword.
        /// </summary>
        private ActionResult Refusal(EmailVerificationResultModel result)
        {
            switch (result.Outcome)
            {
                case EmailVerificationOutcome.Invalid:
                    return UnprocessableEntity(new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    });

                case EmailVerificationOutcome.ProfileRequired:
                    return Keyed(
                        Problem(
                            statusCode: StatusCodes.Status403Forbidden,
                            title: "A registered profile is required.",
                            detail: "Register before changing your email address."),
                        AccountController.ProfileRequiredKeyword);

                case EmailVerificationOutcome.TooSoon:
                    return TooSoon(result.RetryAt!.Value);

                case EmailVerificationOutcome.EmailNotSent:
                    return Keyed(
                        Problem(
                            statusCode: StatusCodes.Status503ServiceUnavailable,
                            title: "The verification email could not be sent.",
                            detail: "Try again in a few minutes."),
                        EmailNotSentKeyword);

                case EmailVerificationOutcome.NothingToVerify:
                    return Keyed(
                        Problem(
                            statusCode: StatusCodes.Status409Conflict,
                            title: "There is no email address to verify.",
                            detail: "Your email address is already verified."),
                        NothingToVerifyKeyword);

                case EmailVerificationOutcome.LinkInvalid:
                    return Keyed(
                        Problem(
                            statusCode: StatusCodes.Status410Gone,
                            title: "This verification link no longer works.",
                            detail: "The link has expired, has already been used, or a newer one was sent. "
                                + "Sign in and choose Resend link on Account Info to get a new one."),
                        LinkInvalidKeyword);

                default:
                    throw new InvalidOperationException($"Unmapped email verification outcome {result.Outcome}.");
            }
        }

        /// <summary>
        /// 429 in the PIN reset's shape: a Retry-After header and a
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
                    title: "A verification email was sent recently.",
                    detail: $"We just sent you a verification email. Check your inbox, or try again in {wait}."),
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
