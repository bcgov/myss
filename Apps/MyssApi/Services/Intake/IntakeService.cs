namespace Myss.Api.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Platform;
    using Myss.Api.Providers;
    using Myss.Api.Services;

    /// <summary>
    /// Applicant-side intake over the intake schema and the platform event
    /// store. The forms module is used through its spec provider and its
    /// validator only, never its tables; the profile check goes through the
    /// registration service the same way.
    /// </summary>
    public class IntakeService : IIntakeService
    {
        /// <summary>The only form this slice serves.</summary>
        public const string FormSpecId = "income-assistance-poc";

        private readonly ILogger<IntakeService> _logger;
        private readonly IntakeDbContext _dbContext;
        private readonly IEventStore _eventStore;
        private readonly IFormSpecProvider _formSpecProvider;
        private readonly IUserProfileService _userProfileService;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntakeService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">Injected intake db context.</param>
        /// <param name="eventStore">Injected event store.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        /// <param name="userProfileService">Injected registration-profile service.</param>
        /// <param name="currentUserAccessor">Injected current user accessor.</param>
        /// <param name="timeProvider">Injected time provider.</param>
        public IntakeService(
            ILogger<IntakeService> logger,
            IntakeDbContext dbContext,
            IEventStore eventStore,
            IFormSpecProvider formSpecProvider,
            IUserProfileService userProfileService,
            ICurrentUserAccessor currentUserAccessor,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _eventStore = eventStore;
            _formSpecProvider = formSpecProvider;
            _userProfileService = userProfileService;
            _currentUserAccessor = currentUserAccessor;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public async Task<IntakeResultModel> CreateAsync(CancellationToken cancellationToken)
        {
            string subject = RequireSubject();
            if (!await _userProfileService.HasProfileAsync(subject, cancellationToken))
            {
                return IntakeResultModel.ProfileRequired();
            }

            FormSpecModel? latest = await _formSpecProvider.GetLatestAsync(FormSpecId, cancellationToken);
            if (latest is null)
            {
                _logger.LogError("No published spec for {FormSpecId}; cannot start an application", FormSpecId);
                return IntakeResultModel.SpecUnavailable();
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            var row = new ApplicationAnswers
            {
                Id = Guid.NewGuid(),
                OwnerSubject = subject,
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
                "Started application {ApplicationId} on {FormSpecId} v{FormSpecVersion}",
                row.Id,
                row.FormSpecId,
                row.FormSpecVersion);

            return IntakeResultModel.Ok(ToModel(row, ApplicationState.Seed, spec: null));
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ApplicationSummaryModel>> ListMineAsync(CancellationToken cancellationToken)
        {
            string subject = RequireSubject();
            List<ApplicationAnswers> rows = await _dbContext.Applications
                .AsNoTracking()
                .Where(a => a.OwnerSubject == subject)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

            IReadOnlyDictionary<Guid, EventStream> streams = await _eventStore.LoadManyAsync(
                rows.Select(r => r.Id).ToList(),
                cancellationToken);

            return rows
                .Select(row => ToSummary(row, ApplicationProjection.Fold(streams[row.Id])))
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<IntakeResultModel> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            ApplicationAnswers? row = await FindOwnAsync(id, track: false, cancellationToken);
            if (row is null)
            {
                return IntakeResultModel.NotFound();
            }

            ApplicationState state = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(row.FormSpecId, row.FormSpecVersion, cancellationToken);
            return IntakeResultModel.Ok(ToModel(row, state, spec));
        }

        /// <inheritdoc/>
        public async Task<IntakeResultModel> SaveAnswersAsync(
            Guid id,
            ApplicationAnswersRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ApplicationAnswers? row = await FindOwnAsync(id, track: true, cancellationToken);
            if (row is null)
            {
                return IntakeResultModel.NotFound();
            }

            ApplicationState state = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            if (!ApplicationProjection.CanSave(state))
            {
                return IntakeResultModel.NotEditable(row.Version);
            }

            if (row.Version != request.Version)
            {
                return IntakeResultModel.StaleVersion(row.Version);
            }

            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(row.FormSpecId, row.FormSpecVersion, cancellationToken);
            if (spec is null)
            {
                return IntakeResultModel.SpecUnavailable();
            }

            // A draft is allowed to be incomplete: the required-field check is
            // submit's. Unknown keys and wrong types are still refused, so the
            // working copy never holds what the form could not have produced.
            IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator
                .Validate(spec.Spec, request.Answers)
                .Where(e => !string.Equals(e.Keyword, ValidationKeywords.FieldRequired, StringComparison.Ordinal))
                .ToList();
            if (errors.Count > 0)
            {
                return IntakeResultModel.Invalid(errors);
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
                // The row moved between the read above and this write: the
                // token in the WHERE clause matched nothing. Same answer as the
                // early check, the current version just is not known here.
                return IntakeResultModel.StaleVersion(currentVersion: null);
            }

            return IntakeResultModel.Ok(ToModel(row, state, spec: null));
        }

        /// <inheritdoc/>
        public async Task<IntakeResultModel> SubmitAsync(
            Guid id,
            ApplicationAnswersRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ApplicationAnswers? row = await FindOwnAsync(id, track: false, cancellationToken);
            if (row is null)
            {
                return IntakeResultModel.NotFound();
            }

            EventStream stream = await _eventStore.LoadAsync(id, cancellationToken);
            ApplicationState state = ApplicationProjection.Fold(stream);
            if (!ApplicationProjection.CanSubmit(state))
            {
                return IntakeResultModel.NotEditable(row.Version);
            }

            if (row.Version != request.Version)
            {
                return IntakeResultModel.StaleVersion(row.Version);
            }

            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(row.FormSpecId, row.FormSpecVersion, cancellationToken);
            if (spec is null)
            {
                return IntakeResultModel.SpecUnavailable();
            }

            IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator.Validate(spec.Spec, request.Answers);
            if (errors.Count > 0)
            {
                // Count only; the values are the reason this failed and are the
                // last thing that should reach a log.
                _logger.LogInformation(
                    "Refused submit of application {ApplicationId}: {ErrorCount} validation error(s)",
                    id,
                    errors.Count);
                return IntakeResultModel.Invalid(errors);
            }

            // One write. The row is not touched: the answers that count from
            // here on are the ones in this event's payload.
            var payload = new SubmittedPayload
            {
                FormSpecVersion = row.FormSpecVersion,
                Answers = request.Answers,
            };
            try
            {
                await _eventStore.AppendAsync(
                    id,
                    stream.Version,
                    [payload.ToEvent()],
                    $"applicant:{row.OwnerSubject}",
                    cancellationToken);
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogInformation(ex, "Submit of application {ApplicationId} lost a race; the caller reloads", id);
                return IntakeResultModel.StreamConflict(row.Version);
            }

            _logger.LogInformation(
                "Submitted application {ApplicationId} on {FormSpecId} v{FormSpecVersion}",
                id,
                row.FormSpecId,
                row.FormSpecVersion);

            ApplicationState submitted = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            return IntakeResultModel.Ok(ToModel(row, submitted, spec: null));
        }

        private string RequireSubject()
        {
            CurrentUser user = _currentUserAccessor.User;
            if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.Subject))
            {
                throw new InvalidOperationException("An authenticated identity is required to work on an application.");
            }

            return user.Subject;
        }

        private Task<ApplicationAnswers?> FindOwnAsync(Guid id, bool track, CancellationToken cancellationToken)
        {
            // Owner is part of the lookup, not a check after it: a row that is
            // not the caller's does not exist, so ids cannot be enumerated.
            string subject = RequireSubject();
            IQueryable<ApplicationAnswers> query = _dbContext.Applications;
            if (!track)
            {
                query = query.AsNoTracking();
            }

            return query.SingleOrDefaultAsync(a => a.Id == id && a.OwnerSubject == subject, cancellationToken);
        }

        private static ApplicationSummaryModel ToSummary(ApplicationAnswers row, ApplicationState state)
        {
            return new ApplicationSummaryModel
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
            };
        }

        private static ApplicationModel ToModel(ApplicationAnswers row, ApplicationState state, FormSpecModel? spec)
        {
            return new ApplicationModel
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
                Answers = state.Submitted?.Answers ?? row.Answers.RootElement.Clone(),
                Spec = spec,
            };
        }
    }
}
