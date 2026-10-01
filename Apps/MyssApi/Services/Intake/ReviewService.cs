namespace Myss.Api.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;
    using Myss.Api.Platform;
    using Myss.Api.Providers;
    using Myss.Api.Services;

    /// <summary>
    /// Worker-side intake over the intake schema and the platform event store.
    /// Reads are not owner-scoped: a worker sees every submitted application.
    /// State is always the fold; the only write is an event append guarded by
    /// the stream version the worker sent.
    /// </summary>
    public class ReviewService : IReviewService
    {
        private readonly ILogger<ReviewService> _logger;
        private readonly IntakeDbContext _dbContext;
        private readonly IEventStore _eventStore;
        private readonly IFormSpecProvider _formSpecProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">Injected intake db context.</param>
        /// <param name="eventStore">Injected event store.</param>
        /// <param name="formSpecProvider">Injected form spec provider.</param>
        /// <param name="currentUserAccessor">Injected current user accessor.</param>
        public ReviewService(
            ILogger<ReviewService> logger,
            IntakeDbContext dbContext,
            IEventStore eventStore,
            IFormSpecProvider formSpecProvider,
            ICurrentUserAccessor currentUserAccessor)
        {
            _logger = logger;
            _dbContext = dbContext;
            _eventStore = eventStore;
            _formSpecProvider = formSpecProvider;
            _currentUserAccessor = currentUserAccessor;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewApplicationSummaryModel>> ListAsync(CancellationToken cancellationToken)
        {
            // Every row, then the fold decides which are submitted. A read model
            // replaces this once volume warrants it (design: deferred).
            List<ApplicationAnswers> rows = await _dbContext.Applications
                .AsNoTracking()
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
            return ReviewResultModel.Ok(ToModel(row, state, spec));
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

            if (!ApplicationProjection.AvailableWorkerActions(state).Contains(action, StringComparer.Ordinal))
            {
                return ReviewResultModel.NotAllowed(stream.Version);
            }

            if (stream.Version != request.StreamVersion)
            {
                return ReviewResultModel.StaleVersion(stream.Version);
            }

            try
            {
                await _eventStore.AppendAsync(
                    id,
                    request.StreamVersion,
                    [new MarkerPayload().ToEvent(eventType)],
                    $"worker:{idir}",
                    cancellationToken);
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogInformation(ex, "Worker action {Action} on application {ApplicationId} lost a race; the caller reloads", action, id);
                return ReviewResultModel.StaleVersion(ex.CurrentVersion);
            }

            _logger.LogInformation("Worker {Idir} applied {Action} to application {ApplicationId}", idir, action, id);

            ApplicationState after = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            return ReviewResultModel.Ok(ToModel(row, after, spec: null));
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
                .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
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
            };
        }

        private static ReviewApplicationModel ToModel(ApplicationAnswers row, ApplicationState state, FormSpecModel? spec)
        {
            return new ReviewApplicationModel
            {
                Id = row.Id,
                ReferenceNumber = ApplicationReference.From(row.Id),
                Status = state.StatusCode,
                SubmittedAt = state.SubmittedAt!.Value,
                StreamVersion = state.Version,
                Answers = state.Submitted!.Answers,
                FormSpecId = row.FormSpecId,
                FormSpecVersion = row.FormSpecVersion,
                Spec = spec,
                AvailableActions = ApplicationProjection.AvailableWorkerActions(state),
            };
        }
    }
}
