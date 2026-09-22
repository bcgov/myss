namespace Icm.Api.Host.Controllers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Host.Contracts;
    using Icm.Api.Host.Services;
    using Icm.Api.Models;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// BC Bus Pass submissions: the route MyssApi posts to.
    /// </summary>
    [Route("v1/bus-pass")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class BusPassController : ControllerBase
    {
        private readonly ILogger<BusPassController> _logger;
        private readonly IBusPassSubmitter _submitter;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassController"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="submitter">Injected submitter.</param>
        public BusPassController(ILogger<BusPassController> logger, IBusPassSubmitter submitter)
        {
            _logger = logger;
            _submitter = submitter;
        }

        /// <summary>
        /// Submits a bus pass request to ICM's workflow.
        /// </summary>
        /// <remarks>
        /// 200 with the workflow's answer, which may be a business rejection
        /// (check <c>errorCode</c>). 400 when the body is not a request. 502, 503
        /// or 504 with a problem body carrying a <c>keyword</c> when no answer
        /// could be obtained; the caller must then assume nothing about whether ICM
        /// has the request.
        /// </remarks>
        /// <param name="request">The request in business terms.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("applications")]
        [EndpointName("SubmitBusPassApplication")]
        [ProducesResponseType(typeof(BusPassApplicationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
        public async Task<ActionResult<BusPassApplicationResponse>> Submit(
            [FromBody] BusPassApplicationRequest request,
            CancellationToken cancellationToken)
        {
            BusPassApplication application = BusPassApplicationRequestMapper.ToApplication(request);

            BusPassResult result;
            try
            {
                result = await _submitter.SubmitAsync(application, cancellationToken);
            }
            catch (IcmUpstreamException ex)
            {
                ObjectResult problem = Problem(
                    statusCode: ex.StatusCode,
                    title: "The request could not be submitted to ICM.",
                    detail: ex.Message);
                var details = (ProblemDetails)problem.Value!;
                details.Extensions["keyword"] = ex.Keyword;
                return problem;
            }

            // Ids and codes only. The echoed name is compared, not logged: a match
            // against a different name than the one submitted is how a mis-keyed
            // identifier shows itself.
            _logger.LogInformation(
                "Bus pass submission {SubmissionKey} ({RequestType}) answered: status {Status}, application {ApplicationNumber}, error code {ErrorCode}, echoed name matches {NameMatches}",
                ForLog(application.SubmissionKey),
                application.RequestType,
                result.Status ?? "-",
                result.ApplicationNumber ?? "-",
                string.IsNullOrWhiteSpace(result.ErrorCode) ? "-" : result.ErrorCode,
                NameMatches(application, result));

            return BusPassApplicationResponse.From(result);
        }

        /// <summary>
        /// The submission key as it may appear in a log line. Model validation
        /// already limits it to letters, digits and hyphens; this keeps only those
        /// regardless, so the value is never able to forge a log entry.
        /// </summary>
        private static string ForLog(string? submissionKey)
        {
            if (string.IsNullOrEmpty(submissionKey))
            {
                return "-";
            }

            char[] kept = [.. submissionKey.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').Take(64)];
            return kept.Length == 0 ? "-" : new string(kept);
        }

        private static bool NameMatches(BusPassApplication application, BusPassResult result)
        {
            return string.Equals(application.FirstName?.Trim(), result.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(application.LastName?.Trim(), result.LastName?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
