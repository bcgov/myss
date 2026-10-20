namespace Myss.AdminApi.Controllers
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
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Services;
    using Myss.FormSpec;

    /// <summary>
    /// The admin rates editor's endpoints, for IDIR staff only: read the table being
    /// edited, and save a new one.
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
        /// Returns the rate table the editor starts from: the latest published table,
        /// read fresh rather than from a cache, or the compiled table when the content
        /// engine holds no usable one. A 502 when the content engine cannot be read:
        /// unlike the citizen read there is no fallback then, so the editor never
        /// offers a table it cannot vouch for. The same shape as MyssApi's anonymous
        /// <c>GET v1/EligibilityEstimator/rates</c>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("GetEligibilityRates")]
        [ProducesResponseType(typeof(BaseResponseModel<EligibilityRatesModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<ActionResult<BaseResponseModel<EligibilityRatesModel>>> Get(
            CancellationToken cancellationToken)
        {
            EligibilityRatesModel rates;
            try
            {
                rates = await _ratesService.GetAsync(cancellationToken);
            }
            catch (ContentEngineUnavailableException ex)
            {
                _logger.LogError(ex, "Reading the eligibility rates failed in the content engine; returning 502.");
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Content engine unavailable",
                    detail: "The rates could not be loaded because the content engine is unavailable. Please try again.");
            }

            return new BaseResponseModel<EligibilityRatesModel>
            {
                Payload = rates,
                DatetimeRequested = DateTime.Now,
            };
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
