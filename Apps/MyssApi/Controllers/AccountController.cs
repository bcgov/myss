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
    using Myss.Api.Configuration;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// The citizen's Account Info (MYSS-271): case and contact details, the
    /// phone numbers, the monthly report reminder and the PIN (MYSS-258) they
    /// may change. Always the caller's own account; there is no id in any route.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/account")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.Client)]
    public class AccountController : ControllerBase
    {
        /// <summary>The caller has no registered profile, so has no account to show.</summary>
        public const string ProfileRequiredKeyword = "ACCOUNT.PROFILE_REQUIRED";

        /// <summary>Too many wrong PINs in a row; Change PIN is refused until the lockout ends.</summary>
        public const string PinLockedKeyword = "ACCOUNT.PIN.LOCKED";

        private readonly IAccountService _accountService;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountController"/> class.
        /// </summary>
        /// <param name="accountService">Injected account service.</param>
        /// <param name="timeProvider">The clock the PIN lockout is measured by.</param>
        public AccountController(IAccountService accountService, TimeProvider timeProvider)
        {
            _accountService = accountService;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Returns the caller's account.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("GetAccount")]
        [ProducesResponseType(typeof(BaseResponseModel<AccountModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<BaseResponseModel<AccountModel>>> Get(CancellationToken cancellationToken)
        {
            return Map(await _accountService.GetAsync(cancellationToken));
        }

        /// <summary>
        /// Replaces the caller's phone numbers. Nothing is saved unless every number passes.
        /// </summary>
        /// <param name="request">The whole list as the citizen left it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPut("phones")]
        [Produces("application/json")]
        [EndpointName("UpdateAccountPhones")]
        [ProducesResponseType(typeof(BaseResponseModel<AccountModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<BaseResponseModel<AccountModel>>> UpdatePhones(
            [FromBody] UpdatePhonesRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _accountService.UpdatePhonesAsync(request, cancellationToken));
        }

        /// <summary>
        /// Saves the caller's notification preference.
        /// </summary>
        /// <param name="request">The preference.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPut("notification-preferences")]
        [Produces("application/json")]
        [EndpointName("UpdateAccountNotificationPreferences")]
        [ProducesResponseType(typeof(BaseResponseModel<AccountModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<BaseResponseModel<AccountModel>>> UpdateNotificationPreferences(
            [FromBody] UpdateNotificationPreferencesRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _accountService.UpdateNotificationPreferencesAsync(request, cancellationToken));
        }

        /// <summary>
        /// Changes the caller's PIN, or creates it when they have none (MYSS-258).
        /// Basic BCeID only. The PIN is never returned or logged, and is not sent
        /// to ICM (RULE-IDA-04).
        /// </summary>
        /// <param name="request">The current PIN, when there is one, and the new one twice.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPut("pin")]
        [Produces("application/json")]
        [EndpointName("SaveAccountPin")]
        [ProducesResponseType(typeof(BaseResponseModel<AccountModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<BaseResponseModel<AccountModel>>> SavePin(
            [FromBody] SavePinRequestModel request,
            CancellationToken cancellationToken)
        {
            return Map(await _accountService.SavePinAsync(request, cancellationToken));
        }

        /// <summary>
        /// One mapping from service outcome to HTTP, in the intake controller's
        /// shapes: 422 carries every field error at once, a refusal is a
        /// ProblemDetails with a stable keyword.
        /// </summary>
        private ActionResult<BaseResponseModel<AccountModel>> Map(AccountResultModel result)
        {
            switch (result.Outcome)
            {
                case AccountOutcome.Ok:
                    return new BaseResponseModel<AccountModel>
                    {
                        Payload = result.Account!,
                        DatetimeRequested = DateTime.Now,
                    };

                case AccountOutcome.Invalid:
                    return UnprocessableEntity(new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    });

                case AccountOutcome.ProfileRequired:
                    ObjectResult problem = Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "A registered profile is required.",
                        detail: "Register before viewing your account.");
                    ((ProblemDetails)problem.Value!).Extensions["keyword"] = ProfileRequiredKeyword;
                    return problem;

                case AccountOutcome.PinNotAvailable:
                    ObjectResult notAvailable = Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "This sign-in does not use a PIN.",
                        detail: "A PIN is only used when you sign in with Basic BCeID.");
                    ((ProblemDetails)notAvailable.Value!).Extensions["keyword"] = ValidationKeywords.PinNotAvailable;
                    return notAvailable;

                case AccountOutcome.PinLocked:
                    return PinLocked(result.LockedUntil!.Value);

                default:
                    throw new InvalidOperationException($"Unmapped account outcome {result.Outcome}.");
            }
        }

        /// <summary>
        /// 429 with the lockout's end, in the shape the bus pass rate limit uses:
        /// a Retry-After header and a ProblemDetails with a stable keyword.
        /// </summary>
        private ObjectResult PinLocked(DateTimeOffset lockedUntil)
        {
            TimeSpan remaining = lockedUntil - _timeProvider.GetUtcNow();
            int minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);

            ObjectResult locked = Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many incorrect PINs.",
                detail: $"You entered an incorrect PIN too many times. Try again in {minutes} minute{(minutes == 1 ? string.Empty : "s")}.");
            var problem = (ProblemDetails)locked.Value!;
            problem.Extensions["keyword"] = PinLockedKeyword;
            problem.Extensions["lockedUntil"] = lockedUntil;
            return locked;
        }
    }
}
