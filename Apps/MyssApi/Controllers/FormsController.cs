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
    using Myss.Api.Configuration;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Myss.FormSpec;
    using Myss.Common.Models;

    /// <summary>
    /// The forms controller: versioned specs and submissions.
    /// Protected by default; <see cref="PubliclyAccessibleFormSpecIds"/> lists the forms
    /// whose spec can be read without signing in (currently just the BC Bus Pass
    /// request, which a citizen fills in without an account). Submissions here are
    /// never anonymous: the public bus pass form submits through
    /// <see cref="BusPassController"/>. Listing and reading stored submissions is
    /// for staff with an IDIR identity: submissions have no owner, so a signed-in
    /// citizen could otherwise read every other applicant's answers.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/forms")]
    [ApiController]
    [Authorize]
    public class FormsController : Controller
    {
        /// <summary>Form specs that can be read without authentication.</summary>
        private static readonly HashSet<string> PubliclyAccessibleFormSpecIds = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "bc-bus-pass",
        };

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
        /// Rejects anonymous callers for any form spec other than a publicly accessible one.
        /// [AllowAnonymous] on the spec action bypasses [Authorize] entirely, so the check
        /// has to be made here instead.
        /// </summary>
        private ActionResult? RequireAuthUnlessPublicForm(string formSpecId)
        {
            if (PubliclyAccessibleFormSpecIds.Contains(formSpecId))
            {
                return null;
            }

            return User.Identity?.IsAuthenticated == true ? null : Unauthorized();
        }

        /// <summary>
        /// Returns the latest published spec for a form (content-engine proxy).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{formSpecId}/spec")]
        [AllowAnonymous]
        [Produces("application/json")]
        [EndpointName("GetFormSpec")]
        [ProducesResponseType(typeof(BaseResponseModel<FormSpecModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BaseResponseModel<FormSpecModel>>> GetSpec(
            string formSpecId,
            CancellationToken cancellationToken
        )
        {
            if (RequireAuthUnlessPublicForm(formSpecId) is ActionResult unauthorized)
            {
                return unauthorized;
            }

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
        /// Returns the error message catalogue: the citizen-facing wording for
        /// every stable error keyword, as published in the content engine.
        /// Anonymous like the public bus pass spec: the catalogue is shared
        /// content, not per-user data, and the public bus pass page reads it to
        /// word the outcomes the API reports by keyword alone.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("error-messages")]
        [AllowAnonymous]
        [Produces("application/json")]
        [EndpointName("GetErrorMessages")]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyDictionary<string, string>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<BaseResponseModel<IReadOnlyDictionary<string, string>>>> GetErrorMessages(
            CancellationToken cancellationToken
        )
        {
            IReadOnlyDictionary<string, string> catalogue = await _formsService.GetErrorMessagesAsync(cancellationToken);

            return new BaseResponseModel<IReadOnlyDictionary<string, string>>
            {
                Payload = catalogue,
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
    }
}
