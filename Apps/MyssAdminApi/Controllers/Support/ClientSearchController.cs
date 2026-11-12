namespace Myss.AdminApi.Support
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Myss.AdminApi.Configuration;
    using Myss.Common.Models;

    /// <summary>
    /// Support View (MYSS-275): a worker searches MySS clients to validate their
    /// personal information. Staff only, with an IDIR identity.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/support/clients")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.WorkerWithIdir)]
    public class ClientSearchController : ControllerBase
    {
        private readonly IClientSearchService _service;

        /// <summary>Initializes a new instance of the <see cref="ClientSearchController"/> class.</summary>
        /// <param name="service">Injected client search service.</param>
        public ClientSearchController(IClientSearchService service)
        {
            _service = service;
        }

        /// <summary>
        /// Searches clients by first name, last name, SIN or BCeID user name, any
        /// one or several together. Names and the BCeID user name match on any
        /// part, ignoring case; the SIN only in full. A result shows the SIN's last
        /// three digits only.
        /// </summary>
        /// <param name="criteria">The search fields, from the query string.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("SearchClients")]
        [ProducesResponseType(typeof(BaseResponseModel<ClientSearchResultModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BaseResponseModel<ClientSearchResultModel>>> Search(
            [FromQuery] ClientSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            ClientSearchOutcome outcome = await _service.SearchAsync(criteria, cancellationToken);
            if (outcome.Result is null)
            {
                ObjectResult problem = Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: outcome.RefusalKeyword == ClientSearchKeywords.SinInvalid
                        ? "Enter the full nine-digit SIN."
                        : "Enter at least one search field.");
                ((ProblemDetails)problem.Value!).Extensions["keyword"] = outcome.RefusalKeyword;
                return problem;
            }

            return new BaseResponseModel<ClientSearchResultModel>
            {
                Payload = outcome.Result,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
