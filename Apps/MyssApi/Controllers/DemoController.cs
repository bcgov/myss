namespace Myss.Api.Controllers
{
    using System;
    using System.Collections.Generic;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// The DemoController controller.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/[controller]")]
    [ApiController]
    public class DemoController : Controller
    {
        private readonly IDemoService _demoService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DemoController"/> class.
        /// </summary>
        /// <param name="demoService">Injected Demo Service.</param>
        public DemoController(IDemoService demoService)
        {
            _demoService = demoService;
        }

        /// <summary>
        /// Returns a placeholder.
        /// </summary>
        [HttpGet]
        [Produces("application/json")]
        [EndpointName("GetPlaceholder")]
        [ProducesResponseType(typeof(BaseResponseModel<DemoModel>), 200)]
        public BaseResponseModel<DemoModel> GetPlaceholder()
        {
            var demoData = _demoService.GetDemo();
            var requestResponse = new BaseResponseModel<DemoModel>()
            {
                Payload = demoData,
                DatetimeRequested = DateTime.Now,
            };

            return requestResponse;
        }
    }
}
