namespace Myss.AdminApi.Intake
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
    using Myss.AdminApi.Services;
    using Myss.Data.Intake;
    using Myss.Data.Platform;
    using Myss.FormSpec;
    using Myss.Common.Auth;
    using Myss.Common.Content;

    /// <summary>
    /// Worker-side intake over the intake schema and the platform event store.
    /// Reads are not owner-scoped: a worker sees every submitted application.
    /// State is always the fold; every write is an event append guarded by
    /// the stream version the worker sent. Picking a file up assigns it to the
    /// caller, and from then until a decision or a release only they can act
    /// on it. An Accept is followed by the promotion step.
    /// </summary>
    public class ReviewService : IReviewService
    {
        private readonly ILogger<ReviewService> _logger;
        private readonly IntakeDbContext _dbContext;
        private readonly IEventStore _eventStore;
        private readonly IFormSpecProvider _formSpecProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IApplicationPromotionProvider _promotionProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">Injected intake db context.</param>
        /// <param name="eventStore">Injected event store.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        /// <param name="currentUserAccessor">Injected current user accessor.</param>
        /// <param name="promotionProvider">Injected promotion provider.</param>
        public ReviewService(
            ILogger<ReviewService> logger,
            IntakeDbContext dbContext,
            IEventStore eventStore,
            IFormSpecProvider formSpecProvider,
            ICurrentUserAccessor currentUserAccessor,
            IApplicationPromotionProvider promotionProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _eventStore = eventStore;
            _formSpecProvider = formSpecProvider;
            _currentUserAccessor = currentUserAccessor;
            _promotionProvider = promotionProvider;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewApplicationSummaryModel>> ListAsync(CancellationToken cancellationToken)
        {
            // Every row, then the fold decides which are submitted. A read model
            // replaces this once volume warrants it (design: deferred).
            List<ApplicationAnswers> rows = await _dbContext.Applications
                .AsNoTracking()
                .Where(a => a.FormSpecId == IntakeFormSpecIds.IncomeAssistance)
                .ToListAsync(cancellationToken);
            IReadOnlyDictionary<Guid, EventStream> streams = await _eventStore.LoadManyAsync(
                rows.Select(r => r.Id).ToList(),
                cancellationToken);

            return rows
                .Select(row => (Row: row, State: ApplicationProjection.Fold(streams[row.Id])))
                .Where(x => x.State.SubmittedAt is not null)
                .OrderByDescending(x => x.State.SubmittedAt)
                .Select(x => ToSummary(x.Row, x.State))
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<ReviewResultModel> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            ApplicationAnswers? row = await FindSubmittedAsync(id, cancellationToken);
            if (row is null)
            {
                return ReviewResultModel.NotFound();
            }

            ApplicationState state = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            if (state.SubmittedAt is null)
            {
                // A draft does not exist as far as a worker is concerned.
                return ReviewResultModel.NotFound();
            }

            FormSpecModel? spec = await _formSpecProvider.GetVersionAsync(row.FormSpecId, row.FormSpecVersion, cancellationToken);
            return ReviewResultModel.Ok(ToModel(row, state, spec, RequireIdir()));
        }

        /// <inheritdoc/>
        public async Task<ReviewResultModel> ActAsync(
            Guid id,
            string action,
            ReviewActionRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            string eventType = ApplicationProjection.EventFor(action);
            ReviewResultModel result = await AppendAsync(
                id,
                request.StreamVersion,
                state => ApplicationProjection.AvailableWorkerActions(state, RequireIdir()).Contains(action, StringComparer.Ordinal),
                new MarkerPayload().ToEvent(eventType),
                action,
                cancellationToken);
            if (result.Outcome != ReviewOutcome.Ok || action != WorkerActions.Accept)
            {
                return result;
            }

            return await PromoteAsync(id);
        }

        /// <inheritdoc/>
        public Task<ReviewResultModel> AddNoteAsync(
            Guid id,
            ReviewNoteRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            return AppendAsync(
                id,
                request.StreamVersion,
                state => ApplicationProjection.CanAddNote(state, RequireIdir()),
                new NotePayload { Text = request.Text.Trim() }.ToEvent(),
                "AddNote",
                cancellationToken);
        }

        /// <summary>
        /// The shared write path: load, refuse what the state or the
        /// assignment does not allow, check the version, append one event.
        /// </summary>
        private async Task<ReviewResultModel> AppendAsync(
            Guid id,
            int streamVersion,
            Func<ApplicationState, bool> allowed,
            DomainEvent evt,
            string action,
            CancellationToken cancellationToken)
        {
            string idir = RequireIdir();

            ApplicationAnswers? row = await FindSubmittedAsync(id, cancellationToken);
            if (row is null)
            {
                return ReviewResultModel.NotFound();
            }

            EventStream stream = await _eventStore.LoadAsync(id, cancellationToken);
            ApplicationState state = ApplicationProjection.Fold(stream);
            if (state.SubmittedAt is null)
            {
                return ReviewResultModel.NotFound();
            }

            if (ApplicationProjection.IsAssignedToAnother(state, idir))
            {
                return ReviewResultModel.AssignedToAnother(stream.Version, state.Assignee);
            }

            if (!allowed(state))
            {
                return ReviewResultModel.NotAllowed(stream.Version);
            }

            if (stream.Version != streamVersion)
            {
                return ReviewResultModel.StaleVersion(stream.Version);
            }

            try
            {
                await _eventStore.AppendAsync(
                    id,
                    streamVersion,
                    [evt],
                    ApplicationActors.Worker(idir),
                    cancellationToken);
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogInformation(ex, "Worker action {Action} on application {ApplicationId} lost a race; the caller reloads", action, id);
                return ReviewResultModel.StaleVersion(ex.CurrentVersion);
            }

            _logger.LogInformation("Worker {Idir} applied {Action} to application {ApplicationId}", idir, action, id);

            ApplicationState after = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            return ReviewResultModel.Ok(ToModel(row, after, spec: null, idir));
        }

        /// <summary>
        /// Hands a just-accepted application on and records the outcome. The
        /// Accept stands either way: a failure is its own status, visible to
        /// workers, never a rollback. Once the Accept is in the log this runs
        /// to the end even if the caller goes away, so an accepted file is
        /// never left without an attempt and a recorded outcome.
        /// </summary>
        private async Task<ReviewResultModel> PromoteAsync(Guid id)
        {
            CancellationToken uncancellable = CancellationToken.None;
            ApplicationAnswers row = (await FindSubmittedAsync(id, uncancellable))!;
            ApplicationState state = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, uncancellable));

            PromotionResult result;
            try
            {
                result = await _promotionProvider.PromoteAsync(
                    new PromotionRequest(
                        id,
                        ApplicationReference.From(id),
                        row.FormSpecId,
                        state.Submitted!.FormSpecVersion,
                        state.Submitted.Answers),
                    uncancellable);
            }
#pragma warning disable CA1031 // Any failure of the step, a timeout included, is a PromotionFailed, never a lost outcome.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Promotion of application {ApplicationId} threw", id);
                result = PromotionResult.Failed("The promotion step raised an error.");
            }

            DomainEvent outcome = result.Succeeded
                ? new MarkerPayload().ToEvent(ApplicationEventTypes.Promoted)
                : new PromotionFailedPayload { Reason = result.Reason ?? "Unknown." }.ToEvent();
            await AppendOutcomeAsync(id, outcome);

            if (result.Succeeded)
            {
                _logger.LogInformation("Promoted application {ApplicationId}", id);
            }
            else
            {
                _logger.LogWarning("Promotion of application {ApplicationId} failed: {Reason}", id, result.Reason);
            }

            ApplicationState after = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, uncancellable));
            return ReviewResultModel.Ok(ToModel(row, after, spec: null, RequireIdir()));
        }

        /// <summary>
        /// Records the promotion outcome at whatever version the stream is at.
        /// Nothing else acts on an accepted file, so a lost race is not
        /// expected; one reload and retry covers it, and a second loss is an
        /// error for the operator because the outcome is then missing.
        /// </summary>
        private async Task AppendOutcomeAsync(Guid id, DomainEvent outcome)
        {
            for (int attempt = 1; ; attempt++)
            {
                EventStream stream = await _eventStore.LoadAsync(id, CancellationToken.None);
                try
                {
                    await _eventStore.AppendAsync(id, stream.Version, [outcome], ApplicationActors.Promotion, CancellationToken.None);
                    return;
                }
                catch (ConcurrencyException ex) when (attempt < 2)
                {
                    _logger.LogWarning(ex, "Recording the promotion outcome of application {ApplicationId} lost a race; retrying", id);
                }
                catch (ConcurrencyException ex)
                {
                    _logger.LogError(ex, "Could not record the promotion outcome of application {ApplicationId}", id);
                    return;
                }
            }
        }

        private string RequireIdir()
        {
            CurrentUser user = _currentUserAccessor.User;
            if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.IdirUsername))
            {
                throw new InvalidOperationException("An IDIR identity is required to review an application.");
            }

            return user.IdirUsername;
        }

        private Task<ApplicationAnswers?> FindSubmittedAsync(Guid id, CancellationToken cancellationToken)
        {
            return _dbContext.Applications
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == id && a.FormSpecId == IntakeFormSpecIds.IncomeAssistance, cancellationToken);
        }

        private static ReviewApplicationSummaryModel ToSummary(ApplicationAnswers row, ApplicationState state)
        {
            return new ReviewApplicationSummaryModel
            {
                Id = row.Id,
                ReferenceNumber = ApplicationReference.From(row.Id),
                Status = state.StatusCode,
                SubmittedAt = state.SubmittedAt!.Value,
                StreamVersion = state.Version,
                Assignee = state.Assignee,
                InQueue = ApplicationProjection.IsInQueue(state),
            };
        }

        private static ReviewApplicationModel ToModel(ApplicationAnswers row, ApplicationState state, FormSpecModel? spec, string idir)
        {
            return new ReviewApplicationModel
            {
                Id = row.Id,
                ReferenceNumber = ApplicationReference.From(row.Id),
                Status = state.StatusCode,
                SubmittedAt = state.SubmittedAt!.Value,
                StreamVersion = state.Version,
                Assignee = state.Assignee,
                InQueue = ApplicationProjection.IsInQueue(state),
                Answers = state.Submitted!.Answers,
                FormSpecId = row.FormSpecId,
                FormSpecVersion = row.FormSpecVersion,
                Spec = spec,
                AvailableActions = ApplicationProjection.AvailableWorkerActions(state, idir),
                CanAddNote = ApplicationProjection.CanAddNote(state, idir),
                Notes = state.Notes
                    .Select(n => new ReviewNoteModel { Text = n.Text, Author = n.AuthorIdir, CreatedAt = n.CreatedAt })
                    .ToList(),
                History = state.History.Select(ToHistoryEntry).ToList(),
            };
        }

        private static ReviewHistoryEntryModel ToHistoryEntry(ApplicationHistoryEntry entry)
        {
            // The applicant's subject stays on the event row; a worker sees
            // that the applicant acted, not their identity token's id.
            string? idir = ApplicationActors.IdirOf(entry.Actor);
            string kind = idir is not null
                ? ReviewHistoryEntryModel.WorkerActor
                : entry.Actor.StartsWith(ApplicationActors.ApplicantPrefix, StringComparison.Ordinal)
                    ? ReviewHistoryEntryModel.ApplicantActor
                    : ReviewHistoryEntryModel.SystemActor;
            return new ReviewHistoryEntryModel
            {
                Code = entry.Code,
                ActorKind = kind,
                Idir = idir,
                OccurredAt = entry.OccurredAt,
            };
        }
    }
}
