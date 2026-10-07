namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
        using System.Globalization;
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

        /// <summary>
        /// The oldest registration version a submission may claim. v1 predates the
        /// fields a profile needs; see <see cref="SubmitAsync(string, FormSubmissionRequestModel, Func{JsonElement, IReadOnlyList{ValidationErrorModel}}?, CancellationToken)"/>.
        /// </summary>
        private const int MinimumRegistrationVersion = 2;

        private readonly ILogger<FormsService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly IFormSpecProvider _formSpecProvider;
        private readonly IPdfProvider _pdfProvider;
        private readonly ITemplateProvider _templateProvider;
        private readonly IFormSpecAdminProvider _formSpecAdminProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IErrorMessageProvider _errorMessageProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormsService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        /// <param name="pdfProvider">Injected PDF provider.</param>
        /// <param name="templateProvider">Injected template provider.</param>
        /// <param name="formSpecAdminProvider">Injected form-spec admin (write) provider.</param>
        /// <param name="currentUserAccessor">Injected current user accessor.</param>
        /// <param name="errorMessageProvider">Injected error message catalogue provider.</param>
        public FormsService(
            ILogger<FormsService> logger,
            FormsDbContext dbContext,
            IFormSpecProvider formSpecProvider,
            IPdfProvider pdfProvider,
            ITemplateProvider templateProvider,
            IFormSpecAdminProvider formSpecAdminProvider,
            ICurrentUserAccessor currentUserAccessor,
            IErrorMessageProvider errorMessageProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _formSpecProvider = formSpecProvider;
            _pdfProvider = pdfProvider;
            _templateProvider = templateProvider;
            _formSpecAdminProvider = formSpecAdminProvider;
            _currentUserAccessor = currentUserAccessor;
            _errorMessageProvider = errorMessageProvider;
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetLatestSpecAsync(string formSpecId, CancellationToken cancellationToken)
        {
            return _formSpecProvider.GetLatestAsync(formSpecId, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyDictionary<string, string>> GetErrorMessagesAsync(CancellationToken cancellationToken)
        {
            return _errorMessageProvider.GetMessagesAsync(cancellationToken);
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
            FormSpecModel? draft = await _formSpecAdminProvider.GetDraftOrLatestPublishedAsync(formSpecId, cancellationToken);
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
        /// message. Unknown keywords are dropped. It is called only after
        /// <see cref="IsLifecycleRefusal"/> has confirmed that at least one recognized
        /// lifecycle keyword is present.
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
        public Task<FormSubmissionResultModel> SubmitAsync(string formSpecId, FormSubmissionRequestModel request, CancellationToken cancellationToken)
        {
            return SubmitAsync(formSpecId, request, domainRules: null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<FormSubmissionResultModel> SubmitAsync(
            string formSpecId,
            FormSubmissionRequestModel request,
            Func<JsonElement, IReadOnlyList<ValidationErrorModel>>? domainRules,
            CancellationToken cancellationToken)
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

            // Registration v1 asked only for a first name, but a profile needs the
            // last name, date of birth, email and SIN that v2 introduced, so a v1
            // answer set can never become a profile. v1 is published and immutable
            // and cannot be retired from Strapi, so it is refused here, the same
            // way as an unknown version: that tells a stale tab to reload onto the
            // current form instead of failing on fields the citizen was never shown.
            if (IsRegistration(formSpecId) && request.FormSpecVersion < MinimumRegistrationVersion)
            {
                _logger.LogWarning(
                    "Rejected registration on retired version {FormSpecVersion}; the minimum is {MinimumVersion}",
                    request.FormSpecVersion,
                    MinimumRegistrationVersion);

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

            if (IsRegistration(formSpecId))
            {
                errors = FormSpecValidator.OnePerField([.. errors, .. ValidateRegistration(spec.Spec, request.Answers)]);
            }

            if (domainRules is not null)
            {
                // The spec's failure for a field comes first; a domain rule adds
                // a field's failure only when the spec found none, so the citizen
                // reads one reason per field, as the form shows them.
                IReadOnlyList<ValidationErrorModel> domainErrors = domainRules(request.Answers);
                if (domainErrors.Count > 0)
                {
                    errors = FormSpecValidator.OnePerField([.. errors, .. domainErrors]);
                }
            }

            if (errors.Count > 0)
            {
                // Count only. The values are the reason this failed and are the
                // last thing that should reach a log.
                _logger.LogInformation(
                    "Rejected submission for {FormSpecId} v{FormSpecVersion}: {ErrorCount} validation error(s)",
                    formSpecId,
                    request.FormSpecVersion,
                    errors.Count);

                // The validators carry compiled default wording. The catalogue
                // a Service Designer publishes in the content engine replaces
                // it keyword by keyword, so what the citizen reads is authored
                // content rather than code. Read only on this path: an accepted
                // submission never needs it.
                return FormSubmissionResultModel.Refused(
                    ErrorMessageResolver.Resolve(
                        errors,
                        await _errorMessageProvider.GetMessagesAsync(cancellationToken)));
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
            if (IsRegistration(formSpecId))
            {
                AddOrUpdateProfile(request.Answers);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Don't log the answers; they may contain PII.
            _logger.LogInformation(
                "Stored submission {SubmissionId} for {FormSpecId} v{FormSpecVersion}",
                submission.Id,
                submission.FormSpecId,
                submission.FormSpecVersion);

            return FormSubmissionResultModel.Accepted(ToResponse(submission, spec: null));
        }

        private static bool IsRegistration(string formSpecId) =>
            string.Equals(formSpecId, "registration", StringComparison.OrdinalIgnoreCase);

        private void AddOrUpdateProfile(JsonElement answers)
        {
            CurrentUser currentUser = _currentUserAccessor.User;
            if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.Subject))
            {
                throw new InvalidOperationException("An authenticated identity is required to register a MySS profile.");
            }

            string firstName = answers.GetProperty("firstName").GetString()!;
            string lastName = answers.GetProperty("lastName").GetString()!;
            string dateOfBirth = answers.GetProperty("dateOfBirth").GetString()!;
            string email = answers.GetProperty("email").GetString()!;
            string sin = answers.GetProperty("sin").GetString()!;

            // Optional at this layer: registration v2 and v3 did not ask for them,
            // and a submission is validated against the version it claims.
            // Stored as the ten digits, by the same rule FormSpecValidator has
            // already held the answer to.
            string? phone = OptionalString(answers, "phone") is { } rawPhone
                && PhoneNumber.TryCreate(rawPhone) is { IsValid: true } validPhone
                    ? validPhone.Value!.Digits
                    : null;
            string? gender = OptionalString(answers, "gender");

            _ = TryParseRegistrationDate(dateOfBirth, out DateOnly parsedDate);

            MyssUserProfile? profile = _dbContext.MyssUserProfiles
                .SingleOrDefault(p => p.Subject == currentUser.Subject);
            if (profile is null)
            {
                _dbContext.MyssUserProfiles.Add(new MyssUserProfile
                {
                    Id = Guid.NewGuid(),
                    Subject = currentUser.Subject,
                    FirstName = firstName,
                    LastName = lastName,
                    DateOfBirth = parsedDate,
                    Email = email,
                    Sin = sin,
                    Phone = phone,
                    Gender = gender,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                return;
            }

            profile.FirstName = firstName;
            profile.LastName = lastName;
            profile.DateOfBirth = parsedDate;
            profile.Email = email;
            profile.Sin = sin;
            // v2 and v3 are still published and do not ask for these, so an absent
            // answer means "not on this form", not "cleared": keep what v4 stored.
            // (v1 never gets this far; SubmitAsync refuses it.)
            if (phone is not null)
            {
                profile.Phone = phone;
            }

            if (gender is not null)
            {
                profile.Gender = gender;
            }

            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        private static IReadOnlyList<ValidationErrorModel> ValidateRegistration(JsonElement spec, JsonElement answers)
        {
            List<ValidationErrorModel> errors = [];
            string? dateOfBirth = OptionalString(answers, "dateOfBirth");

            if (!TryParseRegistrationDate(dateOfBirth ?? string.Empty, out DateOnly parsedDate))
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = "dateOfBirth",
                    Keyword = ValidationKeywords.RegistrationDateOfBirthInvalid,
                    Message = "Enter a valid date of birth.",
                });
            }
            else if (parsedDate > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = "dateOfBirth",
                    Keyword = ValidationKeywords.RegistrationDateOfBirthInFuture,
                    Message = "Date of birth cannot be in the future.",
                });
            }

            // Whether gender is required is the spec's call, already enforced by
            // FormSpecValidator, which also checks the phone (a phoneNumber field
            // gets the PhoneNumber rule there). This only checks a gender that is
            // there. The options are authored in Strapi, so check against the spec the
            // citizen was shown rather than a list held here.
            if (OptionalString(answers, "gender") is { } gender
                && !ComponentOptionValues(spec, "gender").Contains(gender))
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = "gender",
                    Keyword = ValidationKeywords.RegistrationGenderUnknown,
                    Message = "Choose one of the listed options.",
                });
            }

            return errors;
        }

        private static string? OptionalString(JsonElement answers, string key) =>
            answers.TryGetProperty(key, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString())
                    ? value.GetString()
                    : null;

        /// <summary>
        /// The option values of the component keyed <paramref name="key"/>, found
        /// at any depth (a panel holds its fields). Empty when there is no such
        /// component, so an answer for it cannot match.
        /// </summary>
        private static HashSet<string> ComponentOptionValues(JsonElement node, string key)
        {
            HashSet<string> values = [];
            if (node.ValueKind != JsonValueKind.Object
                || !node.TryGetProperty("components", out JsonElement components)
                || components.ValueKind != JsonValueKind.Array)
            {
                return values;
            }

            foreach (JsonElement component in components.EnumerateArray())
            {
                if (component.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (component.TryGetProperty("key", out JsonElement k)
                    && k.ValueKind == JsonValueKind.String
                    && k.GetString() == key
                    && component.TryGetProperty("values", out JsonElement options)
                    && options.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement option in options.EnumerateArray())
                    {
                        if (option.ValueKind == JsonValueKind.Object
                            && option.TryGetProperty("value", out JsonElement v)
                            && v.ValueKind == JsonValueKind.String)
                        {
                            values.Add(v.GetString()!);
                        }
                    }
                }

                values.UnionWith(ComponentOptionValues(component, key));
            }

            return values;
        }

        private static bool TryParseRegistrationDate(string value, out DateOnly date)
        {
            if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset dateTime))
            {
                date = DateOnly.FromDateTime(dateTime.DateTime);
                return true;
            }

            date = default;
            return false;
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
