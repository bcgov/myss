namespace Myss.Api.Platform
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// One row of the shared event log (handbook Part 3.4, ADR-0001). Rows are
    /// inserted and never updated: a file's state is always a fold over its
    /// rows, ordered by <see cref="Version"/>. The primary key on
    /// (<see cref="StreamId"/>, <see cref="Version"/>) is what turns a
    /// concurrent append into a unique violation instead of a lost update,
    /// and it holds across any number of API replicas because Postgres
    /// enforces it, not the application.
    /// </summary>
    public class Event
    {
        /// <summary>
        /// Gets or sets the file this event belongs to (for intake, the
        /// application id). The platform does not know what kind of file a
        /// stream is; the owning domain does.
        /// </summary>
        public Guid StreamId { get; set; }

        /// <summary>
        /// Gets or sets the position of this event in its stream, starting at 1.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the event type name, typed in the owning domain's code.
        /// </summary>
        public required string Type { get; set; }

        /// <summary>
        /// Gets or sets the event payload. It carries an <c>eventVersion</c> so
        /// a later change of shape is handled by an upcaster, never by editing
        /// rows. It may hold PII (an application's answers, for instance) and
        /// must never reach a log.
        /// </summary>
        public required JsonDocument Payload { get; set; }

        /// <summary>
        /// Gets or sets who caused the event, as <c>applicant:{subject}</c> or
        /// later <c>worker:{idir}</c>.
        /// </summary>
        public required string Actor { get; set; }

        /// <summary>
        /// Gets or sets when the event was appended.
        /// </summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>
        /// Gets or sets the correlation id of the request that appended the
        /// event, so one citizen action can be followed from the log into the
        /// request logs.
        /// </summary>
        public string? RequestId { get; set; }
    }
}
