namespace Myss.Api.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
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
    using Myss.Api.Services;

    /// <summary>
    /// BC Bus Pass submission endpoints.
    /// Protected: bus pass submissions are only available after the user has authenticated.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/bus-pass")]
    [ApiController]
    [Authorize]
    public class BusPassController : Controller
    {
        private readonly ILogger<BusPassController> _logger;
        private readonly IFormsService _formsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassController"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="formsService">Injected Forms Service.</param>
        public BusPassController(ILogger<BusPassController> logger, IFormsService formsService)
        {
            _logger = logger;
            _formsService = formsService;
        }

        /// <summary>
        /// Generates the BC Bus Pass request PDF for a stored submission.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("submissions/{id:guid}/pdf")]
        [EndpointName("GetBusPassSubmissionPdf")]
        [Produces("application/pdf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSubmissionPdf(Guid id, CancellationToken cancellationToken)
        {
            byte[]? pdf = await _formsService.GetBusPassSubmissionPdfAsync(id, cancellationToken);
            if (pdf is null)
            {
                return NotFound();
            }

            _logger.LogInformation(
                "Served bus pass PDF for submission {SubmissionId} ({Bytes} bytes)",
                id,
                pdf.Length);

            return File(pdf, "application/pdf", $"bus-pass-{id:N}.pdf");
        }
    }
}
