namespace Myss.Api.Platform
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Services;

    /// <summary>
    /// The event store over <see cref="PlatformDbContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two checks guard an append. The first compares the stream's loaded
    /// version with the caller's expected version and fails early with the
    /// current version, which is the friendly path. The second is the primary
    /// key on (stream_id, version): if two appends pass the first check at the
    /// same moment, the database rejects the second, and that is the guarantee
    /// that holds across API replicas.
    /// </para>
    /// <para>
    /// The database rejection is caught as EF's generic
    /// <see cref="DbUpdateException"/> rather than the Postgres-specific error,
    /// so the same mapping works under Postgres and under the in-memory
    /// provider the test suite uses.
    /// </para>
    /// </remarks>
    public class EventStore : IEventStore
    {
        private readonly PlatformDbContext _dbContext;
        private readonly ICorrelationIdAccessor _correlationIdAccessor;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStore"/> class.
        /// </summary>
        /// <param name="dbContext">Injected platform db context.</param>
        /// <param name="correlationIdAccessor">Injected correlation id accessor.</param>
        /// <param name="timeProvider">Injected time provider.</param>
        public EventStore(
            PlatformDbContext dbContext,
            ICorrelationIdAccessor correlationIdAccessor,
            TimeProvider timeProvider)
        {
            _dbContext = dbContext;
            _correlationIdAccessor = correlationIdAccessor;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public async Task<int> AppendAsync(
            Guid streamId,
            int expectedVersion,
            IReadOnlyList<DomainEvent> events,
            string actor,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(events);
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);
            if (events.Count == 0)
            {
                throw new ArgumentException("At least one event is required.", nameof(events));
            }

            int currentVersion = await _dbContext.Events
                .AsNoTracking()
                .Where(e => e.StreamId == streamId)
                .Select(e => (int?)e.Version)
                .MaxAsync(cancellationToken) ?? 0;
            if (currentVersion != expectedVersion)
            {
                throw new ConcurrencyException(streamId, expectedVersion, currentVersion);
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            string? requestId = _correlationIdAccessor.CorrelationId;
            int version = expectedVersion;
            foreach (DomainEvent domainEvent in events)
            {
                version++;
                _dbContext.Events.Add(new Event
                {
                    StreamId = streamId,
                    Version = version,
                    Type = domainEvent.Type,
                    Payload = JsonDocument.Parse(domainEvent.Payload.GetRawText()),
                    Actor = actor,
                    OccurredAt = now,
                    RequestId = requestId,
                });
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // The constraint fired: another append won between the check
                // above and this write. The current version is unknown here;
                // the caller reloads to find out.
                throw new ConcurrencyException(streamId, expectedVersion, currentVersion: null, ex);
            }

            return version;
        }

        /// <inheritdoc/>
        public async Task<EventStream> LoadAsync(Guid streamId, CancellationToken cancellationToken)
        {
            List<Event> rows = await _dbContext.Events
                .AsNoTracking()
                .Where(e => e.StreamId == streamId)
                .OrderBy(e => e.Version)
                .ToListAsync(cancellationToken);

            return new EventStream(rows.Select(ToStored).ToList());
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyDictionary<Guid, EventStream>> LoadManyAsync(
            IReadOnlyCollection<Guid> streamIds,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(streamIds);
            var result = streamIds.Distinct().ToDictionary(id => id, _ => EventStream.Empty);
            if (result.Count == 0)
            {
                return result;
            }

            List<Guid> ids = result.Keys.ToList();
            List<Event> rows = await _dbContext.Events
                .AsNoTracking()
                .Where(e => ids.Contains(e.StreamId))
                .OrderBy(e => e.StreamId)
                .ThenBy(e => e.Version)
                .ToListAsync(cancellationToken);

            foreach (IGrouping<Guid, Event> group in rows.GroupBy(e => e.StreamId))
            {
                result[group.Key] = new EventStream(group.Select(ToStored).ToList());
            }

            return result;
        }

        private static StoredEvent ToStored(Event row)
        {
            return new StoredEvent(
                row.Version,
                row.Type,
                row.Payload.RootElement.Clone(),
                row.Actor,
                row.OccurredAt);
        }
    }
}
