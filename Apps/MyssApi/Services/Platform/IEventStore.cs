namespace Myss.Api.Platform
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The append-only event store every event-sourced domain reuses
    /// (handbook Part 3.4, ADR-0001). Domains supply their event types, their
    /// fold and their rules; this is the only way they touch the log.
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Appends events to a stream, expecting it to currently be at
        /// <paramref name="expectedVersion"/>. The first event lands at
        /// <c>expectedVersion + 1</c>.
        /// </summary>
        /// <param name="streamId">The file the events belong to.</param>
        /// <param name="expectedVersion">The stream version the caller last saw (0 for a new stream).</param>
        /// <param name="events">The events to append, in order.</param>
        /// <param name="actor">Who caused them.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stream version after the append.</returns>
        /// <exception cref="ConcurrencyException">
        /// The stream is not at <paramref name="expectedVersion"/>: someone
        /// else appended first.
        /// </exception>
        Task<int> AppendAsync(
            Guid streamId,
            int expectedVersion,
            IReadOnlyList<DomainEvent> events,
            string actor,
            CancellationToken cancellationToken);

        /// <summary>
        /// Loads a stream in order.
        /// </summary>
        /// <param name="streamId">The file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The events and the current version (0 for an empty stream).</returns>
        Task<EventStream> LoadAsync(Guid streamId, CancellationToken cancellationToken);

        /// <summary>
        /// Loads several streams in one query, so a list of files folds without
        /// one round trip per row.
        /// </summary>
        /// <param name="streamIds">The files.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A stream per requested id; an id with no events maps to an empty stream.</returns>
        Task<IReadOnlyDictionary<Guid, EventStream>> LoadManyAsync(
            IReadOnlyCollection<Guid> streamIds,
            CancellationToken cancellationToken);
    }
}
