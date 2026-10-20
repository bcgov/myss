namespace Myss.AdminApi.Controllers
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
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Services;

    /// <summary>
    /// The BC Bus Pass request PDF, for staff. Moved from MyssApi's
    /// BusPassController with the rest of the worker side, at the same route. The
    /// PDF carries a name, SIN, date of birth and address, so it is never
    /// anonymous: a signed-in worker with an IDIR identity (WorkerWithIdir).
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/bus-pass")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.WorkerWithIdir)]
    public class BusPassSubmissionsController : Controller
    {
        private readonly ILogger<BusPassSubmissionsController> _logger;
        private readonly IFormSubmissionsService _submissionsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassSubmissionsController"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="submissionsService">Injected submissions service.</param>
        public BusPassSubmissionsController(
            ILogger<BusPassSubmissionsController> logger,
            IFormSubmissionsService submissionsService)
        {
            _logger = logger;
            _submissionsService = submissionsService;
        }

        /// <summary>
        /// Generates the BC Bus Pass request PDF for a stored submission. Staff only:
        /// a signed-in worker with an IDIR identity.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("submissions/{id:guid}/pdf")]
        [EndpointName("GetBusPassSubmissionPdf")]
        [Produces("application/pdf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSubmissionPdf(Guid id, CancellationToken cancellationToken)
        {
            byte[]? pdf = await _submissionsService.GetBusPassSubmissionPdfAsync(id, cancellationToken);
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
