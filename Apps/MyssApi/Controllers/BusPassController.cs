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
        // Matches the .odt's build action in MyssApi.csproj (EmbeddedResource Templates\*.odt);
        // the manifest name is the assembly's root namespace plus the folder and file name.
        private const string TemplateResourceName = "Myss.Api.Templates.bus-pass.odt";

        private readonly ILogger<BusPassController> _logger;
        private readonly IFormsService _formsService;
        private readonly IPdfProvider _pdfProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassController"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="formsService">Injected Forms Service.</param>
        /// <param name="pdfProvider">Injected PDF Provider.</param>
        public BusPassController(ILogger<BusPassController> logger, IFormsService formsService, IPdfProvider pdfProvider)
        {
            _logger = logger;
            _formsService = formsService;
            _pdfProvider = pdfProvider;
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
            FormSubmissionResponseModel? submission = await _formsService.GetBusPassSubmissionForPdfAsync(id, cancellationToken);
            if (submission is null)
            {
                return NotFound();
            }

            byte[] template = LoadTemplate();
            Dictionary<string, object?> data = BusPassPdfDataBuilder.Build(submission.Answers);
            byte[] pdf = await _pdfProvider.GenerateFromOdtAsync(template, data, cancellationToken);

            _logger.LogInformation(
                "Generated bus pass PDF for submission {SubmissionId} ({Bytes} bytes)", id, pdf.Length);

            return File(pdf, "application/pdf", $"bus-pass-{id:N}.pdf");
        }

        private static byte[] LoadTemplate()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream(TemplateResourceName);
            if (stream is null)
            {
                throw new InvalidOperationException(
                    $"Embedded ODT template '{TemplateResourceName}' was not found.");
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
