namespace Myss.Api.Controllers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Common.Content;
    using Myss.Common.Models;

    /// <summary>
    /// The confirmation message a form's page shows after a successful submission.
    /// Anonymous: the public bus pass page reads it before anyone signs in.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/forms")]
    [ApiController]
    [AllowAnonymous]
    public class SubmissionAcknowledgementsController : ControllerBase
    {
        private readonly ISubmissionAcknowledgementProvider _provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubmissionAcknowledgementsController"/> class.
        /// </summary>
        /// <param name="provider">Injected confirmation message provider.</param>
        public SubmissionAcknowledgementsController(ISubmissionAcknowledgementProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Returns a form's live confirmation message. A 404 when none is published or
        /// the form's page shows none; a 502 when the content engine cannot be read.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{formSpecId}/acknowledgement")]
        [Produces("application/json")]
        [EndpointName("GetSubmissionAcknowledgement")]
        [ProducesResponseType(typeof(BaseResponseModel<SubmissionAcknowledgementModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<SubmissionAcknowledgementModel>>> Get(
            string formSpecId,
            CancellationToken cancellationToken)
        {
            if (!SubmissionAcknowledgementForms.Ids.Contains(formSpecId))
            {
                return NotFound();
            }

            SubmissionAcknowledgementLookup lookup = await _provider.GetLiveAsync(formSpecId, cancellationToken);
            if (lookup.Status == SubmissionAcknowledgementStatus.Unavailable)
            {
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Content engine unavailable",
                    detail: "The confirmation message could not be loaded because the content engine is unavailable.");
            }

            if (lookup.Acknowledgement is not SubmissionAcknowledgementModel acknowledgement)
            {
                return NotFound();
            }

            return new BaseResponseModel<SubmissionAcknowledgementModel>
            {
                Payload = acknowledgement,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
