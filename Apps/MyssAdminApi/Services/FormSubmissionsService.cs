namespace Myss.AdminApi.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.Data;
    using Myss.FormSpec;

    /// <summary>
    /// Staff reads of the submissions MyssApi stores in the forms schema, moved
    /// here from MyssApi's FormsService with the worker endpoints. Each read pairs
    /// a submission with the spec version stamped on it, never the latest.
    /// </summary>
    public class FormSubmissionsService : IFormSubmissionsService
    {
        private const string BusPassTemplateName = "bus-pass.odt";

        private readonly ILogger<FormSubmissionsService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly IFormSpecProvider _formSpecProvider;
        private readonly IPdfProvider _pdfProvider;
        private readonly ITemplateProvider _templateProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormSubmissionsService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        /// <param name="pdfProvider">Injected PDF renderer.</param>
        /// <param name="templateProvider">Injected template provider.</param>
        public FormSubmissionsService(
            ILogger<FormSubmissionsService> logger,
            FormsDbContext dbContext,
            IFormSpecProvider formSpecProvider,
            IPdfProvider pdfProvider,
            ITemplateProvider templateProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _formSpecProvider = formSpecProvider;
            _pdfProvider = pdfProvider;
            _templateProvider = templateProvider;
        }

        /// <inheritdoc/>
        public async Task<FormSubmissionResponseModel?> GetSubmissionAsync(Guid id, CancellationToken cancellationToken)
        {
            FormSubmission? submission = await _dbContext.FormSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (submission is null)
            {
                return null;
            }

            // Fetch the version stamped on the submission, not the latest.
            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(
                submission.FormSpecId, submission.FormSpecVersion, cancellationToken);
            if (spec is null)
            {
                // The content engine no longer has a version that a stored
                // submission still references.
                _logger.LogWarning(
                    "Archived spec {FormSpecId} v{FormSpecVersion} not found for submission {SubmissionId}",
                    submission.FormSpecId,
                    submission.FormSpecVersion,
                    submission.Id);
            }

            return ToResponse(submission, spec);
        }

        /// <inheritdoc/>
        public async Task<FormSubmissionResponseModel?> GetBusPassSubmissionForPdfAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            FormSubmission? submission = await _dbContext.FormSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (submission is null || submission.FormSpecId != BusPassPdfFieldMap.FormSpecId)
            {
                return null;
            }

            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(
                submission.FormSpecId,
                submission.FormSpecVersion,
                cancellationToken);
            if (spec is null)
            {
                _logger.LogWarning(
                    "Archived spec {FormSpecId} v{FormSpecVersion} not found for submission {SubmissionId}",
                    submission.FormSpecId,
                    submission.FormSpecVersion,
                    submission.Id);
            }

            return ToResponse(submission, spec);
        }

        /// <inheritdoc/>
        public async Task<byte[]?> GetBusPassSubmissionPdfAsync(Guid id, CancellationToken cancellationToken)
        {
            FormSubmissionResponseModel? submission = await GetBusPassSubmissionForPdfAsync(id, cancellationToken);
            if (submission is null)
            {
                return null;
            }

            byte[] template = await _templateProvider.GetTemplateAsync(BusPassTemplateName, cancellationToken);
            Dictionary<string, object?> data = BusPassPdfDataBuilder.Build(submission.Answers);
            byte[] pdf = await _pdfProvider.GenerateFromOdtAsync(template, data, cancellationToken);

            _logger.LogInformation(
                "Generated bus pass PDF for submission {SubmissionId} ({Bytes} bytes)",
                id,
                pdf.Length);

            return pdf;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<FormSubmissionSummaryModel>> ListSubmissionsAsync(
            string formSpecId,
            CancellationToken cancellationToken
        )
        {
            return await _dbContext
                .FormSubmissions.AsNoTracking()
                .Where(s => s.FormSpecId == formSpecId)
                .OrderByDescending(s => s.SubmittedAt)
                .Select(s => new FormSubmissionSummaryModel
                {
                    Id = s.Id,
                    FormSpecId = s.FormSpecId,
                    FormSpecVersion = s.FormSpecVersion,
                    SubmittedAt = s.SubmittedAt,
                })
                .ToListAsync(cancellationToken);
        }

        private static FormSubmissionResponseModel ToResponse(FormSubmission submission, FormSpecModel? spec)
        {
            return new FormSubmissionResponseModel
            {
                Id = submission.Id,
                FormSpecId = submission.FormSpecId,
                FormSpecVersion = submission.FormSpecVersion,
                Answers = submission.Answers.RootElement.Clone(),
                SubmittedAt = submission.SubmittedAt,
                Spec = spec,
            };
        }
    }
}
