namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Forms module service backed by the forms schema and the content engine.
    /// </summary>
    public class FormsService : IFormsService
    {
        private const string BusPassTemplateName = "bus-pass.odt";

        private readonly ILogger<FormsService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly IFormSpecProvider _formSpecProvider;
        private readonly IPdfProvider _pdfProvider;
        private readonly ITemplateProvider _templateProvider;
        private readonly IFormSpecAdminProvider _formSpecAdminProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormsService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        /// <param name="pdfProvider">Injected PDF provider.</param>
        /// <param name="templateProvider">Injected template provider.</param>
        /// <param name="formSpecAdminProvider">Injected form-spec admin (write) provider.</param>
        public FormsService(
            ILogger<FormsService> logger,
            FormsDbContext dbContext,
            IFormSpecProvider formSpecProvider,
            IPdfProvider pdfProvider,
            ITemplateProvider templateProvider,
            IFormSpecAdminProvider formSpecAdminProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _formSpecProvider = formSpecProvider;
            _pdfProvider = pdfProvider;
            _templateProvider = templateProvider;
            _formSpecAdminProvider = formSpecAdminProvider;
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetLatestSpecAsync(string formSpecId, CancellationToken cancellationToken)
        {
            return _formSpecProvider.GetLatestAsync(formSpecId, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken)
        {
            return _formSpecAdminProvider.ListFormsAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetDraftAsync(string formSpecId, CancellationToken cancellationToken)
        {
            return _formSpecAdminProvider.GetDraftAsync(formSpecId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<FormSpecWriteResultModel<FormSpecModel>> SaveDraftAsync(
            string formSpecId, JsonElement spec, string? title, CancellationToken cancellationToken)
        {
            // Validate, THEN write. This is the one place a spec can be written,
            // so the fast structural check cannot be bypassed by another caller.
            IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator.ValidateSpecStructure(spec);
            if (errors.Count > 0)
            {
                _logger.LogInformation(
                    "Rejected draft save for {FormSpecId}: {ErrorCount} structural error(s)",
                    formSpecId,
                    errors.Count);
                return FormSpecWriteResultModel<FormSpecModel>.Refused(errors);
            }

            try
            {
                FormSpecModel saved = await _formSpecAdminProvider.SaveDraftAsync(formSpecId, spec, title, cancellationToken);
                return FormSpecWriteResultModel<FormSpecModel>.Accepted(saved);
            }
            catch (StrapiWriteException ex) when (IsLifecycleRefusal(ex))
            {
                // A 400 is the Strapi lifecycle refusing the spec on a business rule
                // (duplicate key, version sequence, immutability) - a validation outcome.
                // Any other status (401/403/5xx) is an infrastructure fault and is left to
                // propagate; the controller maps it to 502, not a 422 "invalid form".
                return FormSpecWriteResultModel<FormSpecModel>.Refused(TranslateStrapiRefusal(ex));
            }
        }

        /// <inheritdoc/>
        public async Task<FormSpecWriteResultModel<PublishResultModel>> PublishAsync(
            string formSpecId, CancellationToken cancellationToken)
        {
            // Fast structural re-check on the draft about to go live; the Strapi
            // lifecycle stays the authoritative gate for version + immutability.
            FormSpecModel? draft = await _formSpecAdminProvider.GetDraftAsync(formSpecId, cancellationToken);
            if (draft is not null)
            {
                IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator.ValidateSpecStructure(draft.Spec);
                if (errors.Count > 0)
                {
                    return FormSpecWriteResultModel<PublishResultModel>.Refused(errors);
                }
            }

            try
            {
                int version = await _formSpecAdminProvider.PublishAsync(formSpecId, cancellationToken);
                return FormSpecWriteResultModel<PublishResultModel>.Accepted(new PublishResultModel { Version = version });
            }
            catch (NoDraftToPublishException ex)
            {
                // No in-progress draft to publish - an expected outcome, not a fault.
                return FormSpecWriteResultModel<PublishResultModel>.Refused(
                [
                    new ValidationErrorModel
                    {
                        Field = "formSpecId",
                        Keyword = FormSpecStructureKeywords.NothingToPublish,
                        Message = ex.Message,
                    },
                ]);
            }
            catch (StrapiWriteException ex) when (IsLifecycleRefusal(ex))
            {
                return FormSpecWriteResultModel<PublishResultModel>.Refused(TranslateStrapiRefusal(ex));
            }
        }

        /// <summary>
        /// True only when a Strapi write refusal is the documented lifecycle contract:
        /// an HTTP 400 whose body is the <c>ApplicationError</c> envelope AND carries at
        /// least one keyword from the authoritative vocabulary
        /// (<see cref="FormSpecStructureKeywords.LifecycleKeywords"/>). Any other 400 - a
        /// generic REST/proxy/plugin error, a name-only or keywords-only body, or one whose
        /// keywords are all unrecognized - is treated as an upstream failure, not a form
        /// validation outcome, so its message is never surfaced. Shape-guarded throughout.
        /// </summary>
        /// <param name="ex">The refusal from the admin provider.</param>
        /// <returns>True when this is a trusted lifecycle refusal.</returns>
        private static bool IsLifecycleRefusal(StrapiWriteException ex)
        {
            if (ex.StatusCode != HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(ex.Body))
            {
                return false;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(ex.Body);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("error", out JsonElement error)
                    || error.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                bool isApplicationError = error.TryGetProperty("name", out JsonElement name)
                    && name.ValueKind == JsonValueKind.String
                    && string.Equals(name.GetString(), "ApplicationError", StringComparison.Ordinal);

                return isApplicationError && ExtractRecognizedKeywords(error).Count > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Pulls the recognized lifecycle keywords out of a Strapi <c>error</c> object:
        /// only strings in <see cref="FormSpecStructureKeywords.LifecycleKeywords"/>,
        /// deduplicated in first-seen order. Unknown keywords are dropped; any
        /// non-conforming shape yields an empty list.
        /// </summary>
        /// <param name="error">The Strapi <c>error</c> element.</param>
        /// <returns>The recognized keywords, deduped, first-seen order.</returns>
        private static List<string> ExtractRecognizedKeywords(JsonElement error)
        {
            List<string> recognized = [];
            if (error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("details", out JsonElement details)
                && details.ValueKind == JsonValueKind.Object
                && details.TryGetProperty("keywords", out JsonElement kws)
                && kws.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement kw in kws.EnumerateArray())
                {
                    if (kw.ValueKind == JsonValueKind.String
                        && kw.GetString() is { Length: > 0 } value
                        && FormSpecStructureKeywords.LifecycleKeywords.Contains(value)
                        && !recognized.Contains(value))
                    {
                        recognized.Add(value);
                    }
                }
            }

            return recognized;
        }

        /// <summary>
        /// Turns a trusted Strapi lifecycle refusal into validation errors the client can
        /// show: one error per recognized keyword, deduplicated, carrying Strapi's own
        /// message. Unknown keywords are dropped. Only ever called for a refusal that
        /// <see cref="IsLifecycleRefusal"/> already accepted, so the generic fallback is
        /// defensive.
        /// </summary>
        /// <param name="ex">The refusal from the admin provider (already logged by it).</param>
        /// <returns>One validation error per recognized keyword.</returns>
        private static IReadOnlyList<ValidationErrorModel> TranslateStrapiRefusal(StrapiWriteException ex)
        {
            string? message = null;
            List<string> keywords = [];

            if (!string.IsNullOrWhiteSpace(ex.Body))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(ex.Body);
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("error", out JsonElement error)
                        && error.ValueKind == JsonValueKind.Object)
                    {
                        if (error.TryGetProperty("message", out JsonElement msg)
                            && msg.ValueKind == JsonValueKind.String)
                        {
                            message = msg.GetString();
                        }

                        keywords = ExtractRecognizedKeywords(error);
                    }
                }
                catch (JsonException)
                {
                    // Non-JSON body - fall through to the generic refusal message.
                }
            }

            string safeMessage = string.IsNullOrWhiteSpace(message)
                ? "The content engine refused the change."
                : message!;

            if (keywords.Count == 0)
            {
                return [new ValidationErrorModel
                {
                    Field = "spec",
                    Keyword = FormSpecStructureKeywords.StrapiRefused,
                    Message = safeMessage,
                }];
            }

            return keywords
                .Select(keyword => new ValidationErrorModel
                {
                    Field = "spec",
                    Keyword = keyword,
                    Message = safeMessage,
                })
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<FormSubmissionResultModel> SubmitAsync(string formSpecId, FormSubmissionRequestModel request, CancellationToken cancellationToken)
        {
            // Resolve the version the client claims to have rendered, NOT the
            // latest. §7.2 of the assessment calls this non-negotiable: a
            // citizen part-way through a form when a designer publishes v3 must
            // be validated against the rules they were actually shown.
            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(
                formSpecId, request.FormSpecVersion, cancellationToken);

            if (spec is null)
            {
                _logger.LogWarning(
                    "Rejected submission for {FormSpecId}: claimed version {FormSpecVersion} is unknown or unpublished",
                    formSpecId,
                    request.FormSpecVersion);

                return FormSubmissionResultModel.Refused(
                [
                    new ValidationErrorModel
                    {
                        Field = nameof(FormSubmissionRequestModel.FormSpecVersion),
                        Keyword = ValidationKeywords.VersionUnknown,
                        Message = $"Version {request.FormSpecVersion} of this form is not available. Reload the form and try again.",
                    },
                ]);
            }

            IReadOnlyList<ValidationErrorModel> errors =
                FormSpecValidator.Validate(spec.Spec, request.Answers);

            if (errors.Count > 0)
            {
                // Count only. The values are the reason this failed and are the
                // last thing that should reach a log.
                _logger.LogInformation(
                    "Rejected submission for {FormSpecId} v{FormSpecVersion}: {ErrorCount} validation error(s)",
                    formSpecId,
                    request.FormSpecVersion,
                    errors.Count);

                return FormSubmissionResultModel.Refused(errors);
            }

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

            return FormSubmissionResultModel.Accepted(ToResponse(submission, spec: null));
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
