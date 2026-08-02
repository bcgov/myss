namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Data;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Forms module service backed by the forms schema and the content engine.
    /// </summary>
    public class FormsService : IFormsService
    {
        private readonly ILogger<FormsService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly IFormSpecProvider _formSpecProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormsService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        public FormsService(
            ILogger<FormsService> logger,
            FormsDbContext dbContext,
            IFormSpecProvider formSpecProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _formSpecProvider = formSpecProvider;
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetLatestSpecAsync(string formSpecId, CancellationToken cancellationToken)
        {
            return _formSpecProvider.GetLatestAsync(formSpecId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<FormSubmissionResponseModel> SubmitAsync(string formSpecId, FormSubmissionRequestModel request, CancellationToken cancellationToken)
        {
            var submission = new FormSubmission
            {
                Id = Guid.NewGuid(),
                FormSpecId = formSpecId,
                FormSpecVersion = request.FormSpecVersion,
                Answers = JsonDocument.Parse(request.Answers.GetRawText()),
                SubmittedAt = DateTimeOffset.UtcNow,
            };

            _dbContext.FormSubmissions.Add(submission);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Don't log the answers; they may contain PII.
            _logger.LogInformation(
                "Stored submission {SubmissionId} for {FormSpecId} v{FormSpecVersion}",
                submission.Id,
                submission.FormSpecId,
                submission.FormSpecVersion);

            return ToResponse(submission, spec: null);
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
