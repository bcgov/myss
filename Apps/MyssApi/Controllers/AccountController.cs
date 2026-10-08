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
    using Myss.Api.Configuration;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// The citizen's Account Info (MYSS-271): case and contact details, the
    /// phone numbers and the monthly report reminder they may change. Always
    /// the caller's own account; there is no id in any route.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/account")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.Client)]
    public class AccountController : ControllerBase
    {
        /// <summary>The caller has no registered profile, so has no account to show.</summary>
        public const string ProfileRequiredKeyword = "ACCOUNT.PROFILE_REQUIRED";

        private readonly IAccountService _accountService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountController"/> class.
        /// </summary>
        /// <param name="accountService">Injected account service.</param>
        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
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

                default:
                    throw new InvalidOperationException($"Unmapped account outcome {result.Outcome}.");
            }
        }
    }
}
