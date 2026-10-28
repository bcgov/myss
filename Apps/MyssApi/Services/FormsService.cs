namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.FeatureManagement;
    using Myss.Api.Configuration;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Data;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;
    using Myss.Common.Auth;
    using Myss.Common.Content;

    /// <summary>
    /// Forms module service backed by the forms schema and the content engine.
    /// </summary>
    public class FormsService : IFormsService
    {
        /// <summary>
        /// The oldest registration version a submission may claim. v1 predates the
        /// fields a profile needs; see <see cref="SubmitAsync(string, FormSubmissionRequestModel, Func{JsonElement, IReadOnlyList{ValidationErrorModel}}?, CancellationToken)"/>.
        /// </summary>
        private const int MinimumRegistrationVersion = 2;

        private readonly ILogger<FormsService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly IFormSpecProvider _formSpecProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IErrorMessageProvider _errorMessageProvider;
        private readonly IPinHasher _pinHasher;
        private readonly IEmailVerificationService _emailVerification;
        private readonly IFeatureManager _features;
        private readonly IContactMatchProvider _contactMatch;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormsService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        /// <param name="currentUserAccessor">Injected current user accessor.</param>
        /// <param name="errorMessageProvider">Injected error message catalogue provider.</param>
        /// <param name="pinHasher">Injected PIN hasher, for the PIN set at registration.</param>
        /// <param name="emailVerification">Proves the email address given at registration (MYSS-305).</param>
        /// <param name="features">The feature flags; registration proves its address only with EmailVerification on.</param>
        /// <param name="contactMatch">Matches a new registrant against ICM's contacts.</param>
        public FormsService(
            ILogger<FormsService> logger,
            FormsDbContext dbContext,
            IFormSpecProvider formSpecProvider,
            ICurrentUserAccessor currentUserAccessor,
            IErrorMessageProvider errorMessageProvider,
            IPinHasher pinHasher,
            IEmailVerificationService emailVerification,
            IFeatureManager features,
            IContactMatchProvider contactMatch)
        {
            _logger = logger;
            _dbContext = dbContext;
            _formSpecProvider = formSpecProvider;
            _currentUserAccessor = currentUserAccessor;
            _errorMessageProvider = errorMessageProvider;
            _pinHasher = pinHasher;
            _emailVerification = emailVerification;
            _features = features;
            _contactMatch = contactMatch;
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

            Pin? registrationPin = null;
            bool verifyEmail = false;
            DateTimeOffset? emailVerifiedAt = null;
            if (IsRegistration(formSpecId))
            {
                (IReadOnlyList<ValidationErrorModel> pinErrors, registrationPin) =
                    await ValidateRegistrationPinAsync(request, cancellationToken);
                errors = FormSpecValidator.OnePerField(
                    [.. errors, .. ValidateRegistration(spec.Spec, request.Answers), .. pinErrors]);

                // MYSS-295: with EmailVerification on, the address must have been
                // proven in registration's Step 2 before the profile is created.
                // Checked only when the address itself passed, so the citizen
                // reads one reason for the field.
                verifyEmail = await _features.IsEnabledAsync(FeatureFlags.EmailVerification);
                if (verifyEmail
                    && !errors.Any(e => e.Field == "email")
                    && OptionalString(request.Answers, "email") is { } submittedEmail)
                {
                    emailVerifiedAt = await _emailVerification.RegistrationEmailVerifiedAtAsync(
                        submittedEmail.Trim(),
                        cancellationToken);
                    if (emailVerifiedAt is null)
                    {
                        errors = FormSpecValidator.OnePerField(
                        [
                            .. errors,
                            new ValidationErrorModel
                            {
                                Field = "email",
                                Keyword = ValidationKeywords.RegistrationEmailNotVerified,
                                Message = "Verify your email address before you complete your registration.",
                            },
                        ]);
                    }
                }
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
                await AddOrUpdateProfileAsync(
                    request.Answers,
                    registrationPin is null ? null : _pinHasher.Hash(registrationPin),
                    verifyEmail ? emailVerifiedAt : null,
                    cancellationToken);
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

        /// <param name="answers">The registration answers.</param>
        /// <param name="pinHash">The new PIN's hash, or null to keep the profile's.</param>
        /// <param name="emailVerifiedAt">When the address was proven in Step 2, or null with EmailVerification off.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task AddOrUpdateProfileAsync(
            JsonElement answers,
            string? pinHash,
            DateTimeOffset? emailVerifiedAt,
            CancellationToken cancellationToken)
        {
            CurrentUser currentUser = _currentUserAccessor.User;
            if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.Subject))
            {
                throw new InvalidOperationException("An authenticated identity is required to register a MySS profile.");
            }

            string firstName = answers.GetProperty("firstName").GetString()!;
            string lastName = answers.GetProperty("lastName").GetString()!;
            string dateOfBirth = answers.GetProperty("dateOfBirth").GetString()!;
            // Trimmed as FormSpecValidator checked it, so no stray whitespace is
            // stored or used as a recipient (MYSS-305).
            string email = answers.GetProperty("email").GetString()!.Trim();
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

            MyssUserProfile? profile = await _dbContext.MyssUserProfiles
                .Include(p => p.Phones)
                .SingleOrDefaultAsync(p => p.Subject == currentUser.Subject, cancellationToken);
            if (profile is null)
            {
                // A new registrant is matched against ICM's contacts. For the
                // MVP the match is hardcoded to succeed: the citizen is taken to
                // be known to ICM, so no ICM contact is created here.
                ContactMatchResult match = await _contactMatch.MatchAsync(
                    new ContactMatchCriteria(firstName, lastName, parsedDate, email, sin),
                    cancellationToken);

                // The outcome only; the criteria are PII.
                _logger.LogInformation("ICM contact match for a new registration: {IsMatch}", match.IsMatch);

                profile = new MyssUserProfile
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
                    PinHash = pinHash,
                    IcmContactMatchedAt = match.IsMatch ? DateTimeOffset.UtcNow : null,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                _dbContext.MyssUserProfiles.Add(profile);
                AddRegistrationPhone(profile, phone);
                ApplyEmail(profile, email, emailVerifiedAt);
                return;
            }

            profile.FirstName = firstName;
            profile.LastName = lastName;
            profile.DateOfBirth = parsedDate;
            profile.Sin = sin;
            // v2 and v3 are still published and do not ask for these, so an absent
            // answer means "not on this form", not "cleared": keep what v4 stored.
            // (v1 never gets this far; SubmitAsync refuses it.)
            if (phone is not null)
            {
                profile.Phone = phone;
            }

            AddRegistrationPhone(profile, phone);

            if (gender is not null)
            {
                profile.Gender = gender;
            }

            // Only ever null here when the profile already has a PIN (see
            // ValidateRegistrationPinAsync), which this must not replace.
            if (pinHash is not null)
            {
                profile.PinHash = pinHash;
            }

            profile.UpdatedAt = DateTimeOffset.UtcNow;

            ApplyEmail(profile, email, emailVerifiedAt);
        }

        /// <summary>
        /// With EmailVerification on, the address was proven in Step 2
        /// (MYSS-295) and goes on the profile as verified. Off, it is stored as
        /// given, as before the feature; a different address is not verified.
        /// </summary>
        private void ApplyEmail(MyssUserProfile profile, string email, DateTimeOffset? emailVerifiedAt)
        {
            if (emailVerifiedAt is { } verifiedAt)
            {
                _emailVerification.ApplyVerifiedRegistrationEmail(profile, email, verifiedAt);
                return;
            }

            if (!string.IsNullOrEmpty(profile.Email) && !EmailAddress.ConfirmationMatches(profile.Email, email))
            {
                profile.EmailVerifiedAt = null;
            }

            profile.Email = email;
        }

        /// <summary>
        /// Puts the registration phone on Account Info's list (MYSS-299), as the
        /// Cell number: the form does not ask for a type, and the citizen can
        /// change it there. Only into an empty list: once the citizen has phone
        /// numbers on Account Info, that list is theirs, and registering again
        /// does not add to it or reorder it.
        /// </summary>
        private void AddRegistrationPhone(MyssUserProfile profile, string? phone)
        {
            if (phone is null || profile.Phones.Count > 0)
            {
                return;
            }

            // Added explicitly, as AccountService does: a row whose key is
            // already set would otherwise be taken for an existing one.
            _dbContext.Add(new MyssUserPhone
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                Type = AccountPhoneType.Cell,
                Number = phone,
                Position = 0,
            });
        }

        /// <summary>
        /// The registration PIN (MYSS-258, RULE-IDA-01): required of a Basic BCeID
        /// citizen who has none yet, four digits, typed twice the same. A sign-in
        /// that does not use a PIN may not send one. A profile that already has a
        /// PIN keeps it: registering again is not a way round Change PIN, which
        /// asks for the current one.
        /// </summary>
        private async Task<(IReadOnlyList<ValidationErrorModel> Errors, Pin? Pin)> ValidateRegistrationPinAsync(
            FormSubmissionRequestModel request,
            CancellationToken cancellationToken)
        {
            CurrentUser user = _currentUserAccessor.User;
            bool supplied = request.Pin is not null || request.PinConfirmation is not null;

            if (string.IsNullOrWhiteSpace(user.BceidGuid))
            {
                return supplied
                    ? ([PinError("pin", ValidationKeywords.PinNotAvailable, "A PIN is only used when you sign in with Basic BCeID.")], null)
                    : ([], null);
            }

            bool alreadySet = await _dbContext.MyssUserProfiles
                .AnyAsync(p => p.Subject == user.Subject && p.PinHash != null, cancellationToken);
            if (alreadySet)
            {
                return ([], null);
            }

            DomainValidationResult<Pin> pin = Pin.TryCreate(request.Pin);
            if (!pin.IsValid)
            {
                return ([PinError("pin", pin.Keyword!, pin.Message!)], null);
            }

            if (!string.Equals(request.PinConfirmation, pin.Value!.Digits, StringComparison.Ordinal))
            {
                return ([PinError("pinConfirmation", ValidationKeywords.PinMismatch, "The two PINs do not match.")], null);
            }

            return ([], pin.Value);
        }

        private static ValidationErrorModel PinError(string field, string keyword, string message) =>
            new() { Field = field, Keyword = keyword, Message = message };

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
