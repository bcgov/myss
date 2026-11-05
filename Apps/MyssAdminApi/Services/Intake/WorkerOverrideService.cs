namespace Myss.AdminApi.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.AdminApi.Services;
    using Myss.Common.Auth;
    using Myss.Common.Content;
    using Myss.Data;
    using Myss.Data.Intake;
    using Myss.Data.Platform;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Worker override applications (MYSS-321) over the intake schema and the
    /// platform event store, on the citizen side's pattern (MyssApi's
    /// <c>IntakeService</c>): a working copy under a row version, and a
    /// <c>Submitted</c> event that carries the answers. The differences: the
    /// owner is the client's worker override subject, given to the draft when
    /// it starts so the submit has nothing to move; any worker may open any
    /// draft; and the submit's actor is the worker.
    /// </summary>
    public class WorkerOverrideService : IWorkerOverrideService
    {
        private const string FormSpecId = IntakeFormSpecIds.WorkerOverrideAfa;

        private readonly ILogger<WorkerOverrideService> _logger;
        private readonly IntakeDbContext _dbContext;
        private readonly IEventStore _eventStore;
        private readonly IFormSpecProvider _formSpecProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IWorkerOverrideAccountService _accounts;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerOverrideService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">Injected intake db context.</param>
        /// <param name="eventStore">Injected event store.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        /// <param name="currentUserAccessor">Injected current user accessor.</param>
        /// <param name="accounts">Injected worker override account service.</param>
        /// <param name="timeProvider">Injected time provider.</param>
        public WorkerOverrideService(
            ILogger<WorkerOverrideService> logger,
            IntakeDbContext dbContext,
            IEventStore eventStore,
            IFormSpecProvider formSpecProvider,
            ICurrentUserAccessor currentUserAccessor,
            IWorkerOverrideAccountService accounts,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _eventStore = eventStore;
            _formSpecProvider = formSpecProvider;
            _currentUserAccessor = currentUserAccessor;
            _accounts = accounts;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public async Task<WorkerOverrideResultModel> CreateAsync(CancellationToken cancellationToken)
        {
            string idir = RequireIdir();
            FormSpecModel? latest = await _formSpecProvider.GetLatestAsync(FormSpecId, cancellationToken);
            if (latest is null)
            {
                _logger.LogWarning("No published spec for {FormSpecId}; cannot start a worker override", FormSpecId);
                return WorkerOverrideResultModel.SpecNotPublished();
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            var row = new ApplicationAnswers
            {
                Id = Guid.NewGuid(),
                OwnerSubject = ProfileOrigins.WorkerOverrideSubject(Guid.NewGuid()),
                FormSpecId = FormSpecId,
                FormSpecVersion = latest.Version,
                Answers = JsonDocument.Parse("{}"),
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _dbContext.Applications.Add(row);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Worker {Idir} started worker override application {ApplicationId} on v{FormSpecVersion}",
                idir,
                row.Id,
                row.FormSpecVersion);

            return WorkerOverrideResultModel.Ok(ToModel(row, ApplicationState.Seed, latest, accountId: null));
        }

        /// <inheritdoc/>
        public async Task<WorkerOverrideResultModel> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            RequireIdir();
            ApplicationAnswers? row = await FindAsync(id, track: false, cancellationToken);
            if (row is null)
            {
                return WorkerOverrideResultModel.NotFound();
            }

            ApplicationState state = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(row.FormSpecId, row.FormSpecVersion, cancellationToken);
            Guid? profileId = await _accounts.FindAsync(row.OwnerSubject, cancellationToken);
            return WorkerOverrideResultModel.Ok(ToModel(row, state, spec, profileId));
        }

        /// <inheritdoc/>
        public async Task<WorkerOverrideResultModel> SaveAnswersAsync(
            Guid id,
            WorkerOverrideAnswersRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            RequireIdir();
            ApplicationAnswers? row = await FindAsync(id, track: true, cancellationToken);
            if (row is null)
            {
                return WorkerOverrideResultModel.NotFound();
            }

            ApplicationState state = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            if (!ApplicationProjection.CanSave(state))
            {
                return WorkerOverrideResultModel.NotEditable(row.Version);
            }

            if (row.Version != request.Version)
            {
                return WorkerOverrideResultModel.StaleVersion(row.Version);
            }

            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(row.FormSpecId, row.FormSpecVersion, cancellationToken);
            if (spec is null)
            {
                return WorkerOverrideResultModel.SpecUnavailable();
            }

            // A draft may be incomplete and half-typed, as the citizen's may:
            // the required and format checks are submit's. Unknown keys and
            // wrong types are still refused.
            IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator
                .Validate(spec.Spec, request.Answers)
                .Where(e => string.Equals(e.Keyword, ValidationKeywords.FieldUnknown, StringComparison.Ordinal)
                    || string.Equals(e.Keyword, ValidationKeywords.FieldWrongType, StringComparison.Ordinal))
                .ToList();
            if (errors.Count > 0)
            {
                return WorkerOverrideResultModel.Invalid(errors);
            }

            row.Answers = JsonDocument.Parse(request.Answers.GetRawText());
            row.Version = request.Version + 1;
            row.UpdatedAt = _timeProvider.GetUtcNow();
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return WorkerOverrideResultModel.StaleVersion(currentVersion: null);
            }

            return WorkerOverrideResultModel.Ok(ToModel(row, state, spec: null, accountId: null));
        }

        /// <inheritdoc/>
        public async Task<WorkerOverrideResultModel> SubmitAsync(
            Guid id,
            WorkerOverrideAnswersRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            string idir = RequireIdir();
            ApplicationAnswers? row = await FindAsync(id, track: true, cancellationToken);
            if (row is null)
            {
                return WorkerOverrideResultModel.NotFound();
            }

            EventStream stream = await _eventStore.LoadAsync(id, cancellationToken);
            ApplicationState state = ApplicationProjection.Fold(stream);
            if (!ApplicationProjection.CanSubmit(state))
            {
                return WorkerOverrideResultModel.NotEditable(row.Version);
            }

            if (row.Version != request.Version)
            {
                return WorkerOverrideResultModel.StaleVersion(row.Version);
            }

            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(row.FormSpecId, row.FormSpecVersion, cancellationToken);
            if (spec is null)
            {
                return WorkerOverrideResultModel.SpecUnavailable();
            }

            List<ValidationErrorModel> errors = [.. FormSpecValidator.Validate(spec.Spec, request.Answers)];
            WorkerOverrideClient? client = errors.Count == 0 ? ReadClient(request.Answers, errors) : null;
            if (client is null)
            {
                // Count only; the values are the client's.
                _logger.LogInformation(
                    "Refused submit of worker override application {ApplicationId}: {ErrorCount} validation error(s)",
                    id,
                    errors.Count);
                return WorkerOverrideResultModel.Invalid(errors);
            }

            // The row first, as on the citizen side: it takes the answers and a
            // new version, so of two workers submitting one draft only one gets
            // past here.
            row.Answers = JsonDocument.Parse(request.Answers.GetRawText());
            row.Version = request.Version + 1;
            row.UpdatedAt = _timeProvider.GetUtcNow();
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return WorkerOverrideResultModel.StaleVersion(currentVersion: null);
            }

            // Then the account, before the event: a file in the queue always
            // has its account. Should the event then fail, the account is there
            // with these details and the next submit brings it up to date.
            Guid profileId = await _accounts.SaveAsync(row.OwnerSubject, client, idir, cancellationToken);

            var payload = new SubmittedPayload
            {
                FormSpecVersion = row.FormSpecVersion,
                Answers = request.Answers,
            };
            string eventType = ApplicationProjection.SubmitEventFor(state);
            try
            {
                await _eventStore.AppendAsync(
                    id,
                    stream.Version,
                    [payload.ToEvent(eventType)],
                    ApplicationActors.Worker(idir),
                    cancellationToken);
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogInformation(ex, "Submit of worker override application {ApplicationId} lost a race; the caller reloads", id);
                return WorkerOverrideResultModel.StaleVersion(row.Version);
            }

            _logger.LogInformation(
                "Worker {Idir} {EventType} worker override application {ApplicationId} for profile {ProfileId}",
                idir,
                eventType,
                id,
                profileId);

            ApplicationState submitted = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            return WorkerOverrideResultModel.Ok(ToModel(row, submitted, spec: null, profileId));
        }

        /// <summary>
        /// The client's details from answers the form has already accepted. The
        /// form is authored in Strapi, so a field this needs may be missing from
        /// it, or not carry its rule: each is checked again here, and a gap is a
        /// field error rather than an account with a hole in it.
        /// </summary>
        private static WorkerOverrideClient? ReadClient(JsonElement answers, List<ValidationErrorModel> errors)
        {
            string? firstName = Text(answers, "firstName");
            string? lastName = Text(answers, "lastName");
            string? email = Text(answers, "email");
            string? phone = Text(answers, "phone");
            string? dateOfBirth = Text(answers, "dateOfBirth");
            string? sin = Text(answers, "sin");

            Require(firstName, "firstName", errors);
            Require(lastName, "lastName", errors);

            DomainValidationResult<EmailAddress> validEmail = EmailAddress.TryCreate(email);
            Check(validEmail.IsValid, "email", validEmail.Keyword, validEmail.Message, errors);

            DomainValidationResult<PhoneNumber> validPhone = PhoneNumber.TryCreate(phone);
            Check(validPhone.IsValid, "phone", validPhone.Keyword, validPhone.Message, errors);

            DomainValidationResult<Sin> validSin = Sin.TryCreate(sin);
            Check(validSin.IsValid, "sin", validSin.Keyword, validSin.Message, errors);

            if (!IsoDate.TryParse(dateOfBirth, out DateOnly birthDate))
            {
                errors.Add(Error("dateOfBirth", ValidationKeywords.DateInvalid, "Enter a valid date."));
            }
            else if (birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                errors.Add(Error(
                    "dateOfBirth",
                    ValidationKeywords.RegistrationDateOfBirthInFuture,
                    "Date of birth cannot be in the future."));
            }

            if (errors.Count > 0)
            {
                return null;
            }

            return new WorkerOverrideClient(
                firstName!.Trim(),
                lastName!.Trim(),
                birthDate,
                validEmail.Value!.Value,
                validPhone.Value!.Digits,
                validSin.Value!.Digits);
        }

        private static string? Text(JsonElement answers, string key) =>
            answers.ValueKind == JsonValueKind.Object
                && answers.TryGetProperty(key, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString())
                    ? value.GetString()
                    : null;

        private static void Require(string? value, string field, List<ValidationErrorModel> errors)
        {
            if (value is null)
            {
                errors.Add(Error(field, ValidationKeywords.FieldRequired, "This field is required."));
            }
        }

        private static void Check(bool valid, string field, string? keyword, string? message, List<ValidationErrorModel> errors)
        {
            if (!valid)
            {
                errors.Add(Error(field, keyword ?? ValidationKeywords.FieldRequired, message ?? "This field is required."));
            }
        }

        private static ValidationErrorModel Error(string field, string keyword, string message) =>
            new() { Field = field, Keyword = keyword, Message = message };

        private string RequireIdir()
        {
            CurrentUser user = _currentUserAccessor.User;
            if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.IdirUsername))
            {
                throw new InvalidOperationException("An IDIR identity is required for a worker override.");
            }

            return user.IdirUsername;
        }

        private Task<ApplicationAnswers?> FindAsync(Guid id, bool track, CancellationToken cancellationToken)
        {
            // Not owner-scoped: every worker may open every worker override
            // application. Never any other form's, though.
            IQueryable<ApplicationAnswers> query = _dbContext.Applications;
            if (!track)
            {
                query = query.AsNoTracking();
            }

            return query.SingleOrDefaultAsync(a => a.Id == id && a.FormSpecId == FormSpecId, cancellationToken);
        }

        private static WorkerOverrideApplicationModel ToModel(
            ApplicationAnswers row,
            ApplicationState state,
            FormSpecModel? spec,
            Guid? accountId)
        {
            return new WorkerOverrideApplicationModel
            {
                Id = row.Id,
                ReferenceNumber = ApplicationReference.From(row.Id),
                Status = state.StatusCode,
                Version = row.Version,
                FormSpecId = row.FormSpecId,
                FormSpecVersion = row.FormSpecVersion,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt,
                SubmittedAt = state.SubmittedAt,
                Answers = row.Answers.RootElement.Clone(),
                Spec = spec,
                AccountId = accountId,
            };
        }
    }
}
