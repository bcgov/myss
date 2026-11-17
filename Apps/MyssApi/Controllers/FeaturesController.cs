namespace Myss.Api.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.FeatureManagement;
    using Myss.Api.Configuration;
    using Myss.Common.Models;

    /// <summary>
    /// Which citizen features are on in this environment, so the webclient can
    /// hide the pages and links of the ones that are off. Anonymous: the home
    /// page needs it before sign-in, and the values are not secret. The
    /// endpoints themselves enforce the flags (<c>[FeatureGate]</c>); this is
    /// only what the browser shows.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/features")]
    [ApiController]
    [AllowAnonymous]
    public class FeaturesController : ControllerBase
    {
        private readonly IFeatureManager _featureManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeaturesController"/> class.
        /// </summary>
        /// <param name="featureManager">Injected feature manager.</param>
        public FeaturesController(IFeatureManager featureManager)
        {
            _featureManager = featureManager;
        }

        /// <summary>
        /// Returns every feature flag and whether it is on.
        /// </summary>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("GetFeatures")]
        [ProducesResponseType(typeof(BaseResponseModel<IReadOnlyDictionary<string, bool>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<BaseResponseModel<IReadOnlyDictionary<string, bool>>>> Get()
        {
            Dictionary<string, bool> features = [];
            foreach (string flag in FeatureFlags.All)
            {
                features[flag] = await _featureManager.IsEnabledAsync(flag);
            }

            return new BaseResponseModel<IReadOnlyDictionary<string, bool>>
            {
                Payload = features,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
