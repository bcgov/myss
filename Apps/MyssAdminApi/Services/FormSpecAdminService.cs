namespace Myss.AdminApi.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// The admin form editor's service, backed by the content engine. Every spec is
    /// checked with the same <see cref="FormSpecValidator"/> the citizen API later
    /// validates answers against, before anything is written.
    /// </summary>
    public class FormSpecAdminService : IFormSpecAdminService
    {
        private readonly ILogger<FormSpecAdminService> _logger;
        private readonly IFormSpecAdminProvider _formSpecAdminProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormSpecAdminService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="formSpecAdminProvider">Injected form-spec admin (write) provider.</param>
        public FormSpecAdminService(
            ILogger<FormSpecAdminService> logger,
            IFormSpecAdminProvider formSpecAdminProvider)
        {
            _logger = logger;
            _formSpecAdminProvider = formSpecAdminProvider;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken)
        {
            return _formSpecAdminProvider.ListFormsAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetDraftOrLatestPublishedAsync(string formSpecId, CancellationToken cancellationToken)
        {
            return _formSpecAdminProvider.GetDraftOrLatestPublishedAsync(formSpecId, cancellationToken);
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
                    ForLog(formSpecId),
                    errors.Count);
                return FormSpecWriteResultModel.Refused<FormSpecModel>(errors);
            }

            try
            {
                FormSpecModel saved = await _formSpecAdminProvider.SaveDraftAsync(formSpecId, spec, title, cancellationToken);
                return FormSpecWriteResultModel.Accepted<FormSpecModel>(saved);
            }
            catch (StrapiWriteException ex) when (IsLifecycleRefusal(ex))
            {
                // A 400 is the Strapi lifecycle refusing the spec on a business rule
                // (duplicate key, version sequence, immutability) - a validation outcome.
                // Any other status (401/403/5xx) is an infrastructure fault and is left to
                // propagate; the controller maps it to 502, not a 422 "invalid form".
                return FormSpecWriteResultModel.Refused<FormSpecModel>(TranslateStrapiRefusal(ex));
            }
        }

        /// <inheritdoc/>
        public async Task<FormSpecWriteResultModel<PublishResultModel>> PublishAsync(
            string formSpecId, CancellationToken cancellationToken)
        {
            // Fast structural re-check on the draft about to go live; the Strapi
            // lifecycle stays the authoritative gate for version + immutability.
            FormSpecModel? draft = await _formSpecAdminProvider.GetDraftOrLatestPublishedAsync(formSpecId, cancellationToken);
            if (draft is not null)
            {
                IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator.ValidateSpecStructure(draft.Spec);
                if (errors.Count > 0)
                {
                    return FormSpecWriteResultModel.Refused<PublishResultModel>(errors);
                }
            }

            try
            {
                int version = await _formSpecAdminProvider.PublishAsync(formSpecId, cancellationToken);
                return FormSpecWriteResultModel.Accepted<PublishResultModel>(new PublishResultModel { Version = version });
            }
            catch (NoDraftToPublishException ex)
            {
                // No in-progress draft to publish - an expected outcome, not a fault.
                return FormSpecWriteResultModel.Refused<PublishResultModel>(
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
                return FormSpecWriteResultModel.Refused<PublishResultModel>(TranslateStrapiRefusal(ex));
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
        /// message. Unknown keywords are dropped. It is called only after
        /// <see cref="IsLifecycleRefusal"/> has confirmed that at least one recognized
        /// lifecycle keyword is present.
        /// </summary>
        /// <param name="ex">The refusal from the admin provider (already logged by it).</param>
        /// <returns>One validation error per recognized keyword.</returns>
        private static List<ValidationErrorModel> TranslateStrapiRefusal(StrapiWriteException ex)
        {
            string? message = null;
            List<string> keywords = [];

            // No JsonException handling here: the only callers reach this after
            // IsLifecycleRefusal, which has already parsed this same body as JSON
            // (and returns false, never this path, for one that is not).
            if (!string.IsNullOrWhiteSpace(ex.Body))
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

            string safeMessage = string.IsNullOrWhiteSpace(message)
                ? "The content engine refused the change."
                : message;

            return keywords
                .Select(keyword => new ValidationErrorModel
                {
                    Field = "spec",
                    Keyword = keyword,
                    Message = safeMessage,
                })
                .ToList();
        }

        /// <summary>
        /// A route value made safe to log: line breaks removed, so a crafted id cannot
        /// start a fake log entry.
        /// </summary>
        private static string ForLog(string value)
        {
            return value.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Replace("\n", string.Empty, StringComparison.Ordinal);
        }
    }
}
