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
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// The public, anonymous Pre-Eligibility Estimator gateway. It serves the
    /// Form.io spec and the rate table; it performs NO calculation (Option B —
    /// the browser computes the estimate) and persists nothing.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/EligibilityEstimator")]
    [ApiController]
    [AllowAnonymous]
    public class EligibilityEstimatorController : Controller
    {
        /// <summary>The logical id the estimator form is served under.</summary>
        private const string EstimatorFormSpecId = "eligibility-estimator";

        private readonly ILogger<EligibilityEstimatorController> _logger;

        private readonly IFormSpecProvider _formSpecProvider;

        private readonly IEligibilityRateProvider _rateProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EligibilityEstimatorController"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="formSpecProvider">Injected form-spec provider.</param>
        /// <param name="rateProvider">Injected eligibility-rate provider.</param>
        public EligibilityEstimatorController(
            ILogger<EligibilityEstimatorController> logger,
            IFormSpecProvider formSpecProvider,
            IEligibilityRateProvider rateProvider)
        {
            _logger = logger;
            _formSpecProvider = formSpecProvider;
            _rateProvider = rateProvider;
        }

        /// <summary>
        /// Returns the latest published estimator form spec (content-engine proxy).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("spec")]
        [Produces("application/json")]
        [EndpointName("GetEstimatorSpec")]
        [ProducesResponseType(typeof(BaseResponseModel<FormSpecModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BaseResponseModel<FormSpecModel>>> GetSpec(
            CancellationToken cancellationToken)
        {
            FormSpecModel? spec = await _formSpecProvider.GetLatestAsync(
                EstimatorFormSpecId,
                cancellationToken);
            if (spec is null)
            {
                _logger.LogWarning("No published spec found for {FormSpecId}", EstimatorFormSpecId);
                return NotFound();
            }

            return new BaseResponseModel<FormSpecModel>
            {
                Payload = spec,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Returns the rate table the browser computes the estimate against.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("rates")]
        [Produces("application/json")]
        [EndpointName("GetEstimatorRates")]
        [ProducesResponseType(typeof(BaseResponseModel<EligibilityRatesModel>), StatusCodes.Status200OK)]
        public async Task<ActionResult<BaseResponseModel<EligibilityRatesModel>>> GetRates(
            CancellationToken cancellationToken)
        {
            EligibilityRatesModel rates = await _rateProvider.GetRatesAsync(cancellationToken);

            return new BaseResponseModel<EligibilityRatesModel>
            {
                Payload = rates,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
