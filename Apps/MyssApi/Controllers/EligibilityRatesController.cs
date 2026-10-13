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
    using Myss.Api.Providers;
    using Myss.Api.Services;

    /// <summary>
    /// The admin rates editor's write endpoint, for IDIR staff only. Kept apart from
    /// <see cref="EligibilityEstimatorController"/>, whose class-level
    /// <c>[AllowAnonymous]</c> would skip this policy.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/EligibilityRates")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.AdminIdir)]
    public class EligibilityRatesController : Controller
    {
        private readonly ILogger<EligibilityRatesController> _logger;
        private readonly IEligibilityRatesService _ratesService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EligibilityRatesController"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="ratesService">Injected rates service.</param>
        public EligibilityRatesController(
            ILogger<EligibilityRatesController> logger,
            IEligibilityRatesService ratesService)
        {
            _logger = logger;
            _ratesService = ratesService;
        }

        /// <summary>
        /// Saves a complete rate table as the table effective today, and publishes it.
        /// </summary>
        /// <param name="request">The complete table: seven income rows and the four asset limits.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost]
        [Produces("application/json")]
        [EndpointName("SaveEligibilityRates")]
        [ProducesResponseType(typeof(BaseResponseModel<EligibilityRatesModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyList<ValidationErrorModel>>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<EligibilityRatesModel>>> Save(
            [FromBody] SaveEligibilityRatesRequestModel request,
            CancellationToken cancellationToken)
        {
            EligibilityRatesWriteResultModel result;
            try
            {
                result = await _ratesService.SaveAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is StrapiWriteException or ContentEngineUnavailableException)
            {
                _logger.LogError(ex, "Saving the eligibility rates failed in the content engine; returning 502.");
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Content engine unavailable",
                    detail: "The rates could not be saved because the content engine is unavailable. Please try again.");
            }

            if (!result.IsValid)
            {
                return UnprocessableEntity(
                    new BaseResponseModel<IReadOnlyList<ValidationErrorModel>>
                    {
                        Payload = result.Errors,
                        DatetimeRequested = DateTime.Now,
                    });
            }

            return new BaseResponseModel<EligibilityRatesModel>
            {
                Payload = result.Rates,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
