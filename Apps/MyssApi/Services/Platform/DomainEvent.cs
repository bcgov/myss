namespace Myss.Api.Platform
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    /// An event as a domain hands it to the store or reads it back: a type name
    /// and a JSON payload. Domains wrap this in their own typed unions.
    /// </summary>
    /// <param name="Type">The event type name.</param>
    /// <param name="Payload">The payload; must carry <c>eventVersion</c>.</param>
    public sealed record DomainEvent(string Type, JsonElement Payload);

    /// <summary>
    /// A stored event with the bookkeeping the store added.
    /// </summary>
    /// <param name="Version">Position in the stream, from 1.</param>
    /// <param name="Type">The event type name.</param>
    /// <param name="Payload">The payload.</param>
    /// <param name="Actor">Who caused it.</param>
    /// <param name="OccurredAt">When it was appended.</param>
    public sealed record StoredEvent(
        int Version,
        string Type,
        JsonElement Payload,
        string Actor,
        DateTimeOffset OccurredAt);

    /// <summary>
    /// A loaded stream: its events in order and its current version.
    /// </summary>
    /// <param name="Events">The events, oldest first.</param>
    public sealed record EventStream(IReadOnlyList<StoredEvent> Events)
    {
        /// <summary>
        /// Gets the empty stream.
        /// </summary>
        public static EventStream Empty { get; } = new([]);

        /// <summary>
        /// Gets the current version: the last event's, or 0 when empty.
        /// </summary>
        public int Version => Events.Count == 0 ? 0 : Events[^1].Version;

        /// <summary>
        /// Folds the stream into a state. State is always a pure fold of the
        /// log: same events, same state.
        /// </summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <param name="seed">The state of an empty stream.</param>
        /// <param name="step">The domain's fold step.</param>
        /// <returns>The folded state.</returns>
        public TState Project<TState>(TState seed, Func<TState, StoredEvent, TState> step)
        {
            return Events.Aggregate(seed, step);
        }
    }

    /// <summary>
    /// Raised when an append finds the stream past the caller's expected
    /// version: someone else appended first, and the caller must reload and
    /// decide again. Surfaces as 409 with keyword
    /// <see cref="PlatformKeywords.EventStoreConflict"/>.
    /// </summary>
    public sealed class ConcurrencyException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="ConcurrencyException"/> class.</summary>
        public ConcurrencyException()
            : base("The stream changed; reload and retry.")
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ConcurrencyException"/> class.</summary>
        /// <param name="message">The message.</param>
        public ConcurrencyException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ConcurrencyException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The cause.</param>
        public ConcurrencyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
        /// </summary>
        /// <param name="streamId">The stream.</param>
        /// <param name="expectedVersion">The version the caller expected.</param>
        /// <param name="currentVersion">The version the stream is at, when known.</param>
        public ConcurrencyException(Guid streamId, int expectedVersion, int? currentVersion)
            : this(streamId, expectedVersion, currentVersion, innerException: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyException"/> class
        /// from a database constraint violation.
        /// </summary>
        /// <param name="streamId">The stream.</param>
        /// <param name="expectedVersion">The version the caller expected.</param>
        /// <param name="currentVersion">The version the stream is at, when known.</param>
        /// <param name="innerException">The database exception, when the constraint fired.</param>
        public ConcurrencyException(Guid streamId, int expectedVersion, int? currentVersion, Exception? innerException)
            : base($"Stream {streamId} is not at version {expectedVersion}; reload and retry.", innerException)
        {
            StreamId = streamId;
            ExpectedVersion = expectedVersion;
            CurrentVersion = currentVersion;
        }

        /// <summary>
        /// Gets the stream that moved.
        /// </summary>
        public Guid StreamId { get; }

        /// <summary>
        /// Gets the version the caller expected.
        /// </summary>
        public int ExpectedVersion { get; }

        /// <summary>
        /// Gets the version the stream is actually at, when the store could
        /// tell. Null when the conflict came from the database constraint.
        /// </summary>
        public int? CurrentVersion { get; }
    }

    /// <summary>
    /// Stable error keywords owned by the platform package (DOMAIN.CONTEXT.NAME).
    /// </summary>
    public static class PlatformKeywords
    {
        /// <summary>An append lost the race with another append to the same stream.</summary>
        public const string EventStoreConflict = "PLATFORM.EVENTSTORE.CONFLICT";
    }
}
