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
    /// The texts of a form message, such as the confirmation a form's page shows after
    /// a successful submission. Anonymous: a form's public page reads it before anyone
    /// signs in.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/forms")]
    [ApiController]
    [AllowAnonymous]
    public class FormMessagesController : ControllerBase
    {
        private readonly IFormMessageProvider _provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormMessagesController"/> class.
        /// </summary>
        /// <param name="provider">Injected form message provider.</param>
        public FormMessagesController(IFormMessageProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Returns a form message's live texts. A 404 when none is published or the
        /// message is not defined; a 502 when the content engine cannot be read.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="messageKey">The message's key within the form.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{formSpecId}/messages/{messageKey}")]
        [Produces("application/json")]
        [EndpointName("GetFormMessage")]
        [ProducesResponseType(typeof(BaseResponseModel<FormMessageModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<FormMessageModel>>> Get(
            string formSpecId,
            string messageKey,
            CancellationToken cancellationToken)
        {
            if (FormMessageDefinitions.Find(formSpecId, messageKey) is null)
            {
                return NotFound();
            }

            FormMessageLookup lookup = await _provider.GetLiveAsync(formSpecId, messageKey, cancellationToken);
            if (lookup.Status == FormMessageStatus.Unavailable)
            {
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Content engine unavailable",
                    detail: "The form message could not be loaded because the content engine is unavailable.");
            }

            if (lookup.Message is not FormMessageModel message)
            {
                return NotFound();
            }

            return new BaseResponseModel<FormMessageModel>
            {
                Payload = message,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
