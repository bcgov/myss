namespace Myss.Data.Intake
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Myss.Data.Platform;

    /// <summary>
    /// The intake module's event types: the applicant's submit and resubmit,
    /// the worker's pick-up, notes and decisions, and the promotion outcome
    /// that follows an acceptance.
    /// </summary>
    public static class ApplicationEventTypes
    {
        /// <summary>The applicant submitted the application.</summary>
        public const string Submitted = "Submitted";

        /// <summary>
        /// The first worker slice's Under Review marker, from before
        /// assignment existed. No longer written; folded as a pick-up by the
        /// worker who appended it, so files reviewed under that slice keep
        /// their state.
        /// </summary>
        public const string ReviewStarted = "ReviewStarted";

        /// <summary>A worker picked the application up from the queue. The actor is the assignee.</summary>
        public const string PickedUp = "PickedUp";

        /// <summary>The assignee put the application back in the queue without deciding it.</summary>
        public const string Released = "Released";

        /// <summary>The assignee added an internal note. Workers only; never shown to the applicant.</summary>
        public const string NoteAdded = "NoteAdded";

        /// <summary>The assignee asked the applicant for more information; it is returned to them.</summary>
        public const string MoreInfoRequested = "MoreInfoRequested";

        /// <summary>The applicant submitted a returned application again.</summary>
        public const string Resubmitted = "Resubmitted";

        /// <summary>The assignee escalated the application for a further review.</summary>
        public const string Escalated = "Escalated";

        /// <summary>A worker accepted the application. Promotion follows.</summary>
        public const string Accepted = "Accepted";

        /// <summary>A worker denied the application. Terminal.</summary>
        public const string Denied = "Denied";

        /// <summary>An accepted application was handed on to the case management system.</summary>
        public const string Promoted = "Promoted";

        /// <summary>Handing an accepted application on failed.</summary>
        public const string PromotionFailed = "PromotionFailed";
    }

    /// <summary>
    /// The actor strings the intake module writes on its events, and the one
    /// place that reads a worker's IDIR back out of them.
    /// </summary>
    public static class ApplicationActors
    {
        /// <summary>The prefix of a worker actor.</summary>
        public const string WorkerPrefix = "worker:";

        /// <summary>The prefix of an applicant actor.</summary>
        public const string ApplicantPrefix = "applicant:";

        /// <summary>The actor of events the API appends on its own behalf.</summary>
        public const string Promotion = "system:promotion";

        /// <summary>
        /// Builds a worker actor.
        /// </summary>
        /// <param name="idir">The worker's IDIR username.</param>
        /// <returns>"worker:{idir}".</returns>
        public static string Worker(string idir)
        {
            return WorkerPrefix + idir;
        }

        /// <summary>
        /// Builds an applicant actor.
        /// </summary>
        /// <param name="subject">The applicant's Keycloak subject.</param>
        /// <returns>"applicant:{subject}".</returns>
        public static string Applicant(string subject)
        {
            return ApplicantPrefix + subject;
        }

        /// <summary>
        /// Reads the IDIR out of a worker actor.
        /// </summary>
        /// <param name="actor">An event's actor.</param>
        /// <returns>The IDIR, or null when the actor is not a worker.</returns>
        public static string? IdirOf(string actor)
        {
            ArgumentNullException.ThrowIfNull(actor);
            return actor.StartsWith(WorkerPrefix, StringComparison.Ordinal) ? actor[WorkerPrefix.Length..] : null;
        }
    }

    /// <summary>
    /// The payload of an event that carries nothing but its shape version:
    /// the worker status moves. Who and when live on the event row itself.
    /// </summary>
    public sealed class MarkerPayload
    {
        /// <summary>The payload shape version, for upcasting later.</summary>
        public const int CurrentEventVersion = 1;

        /// <summary>
        /// Gets the payload shape version.
        /// </summary>
        [JsonPropertyName("eventVersion")]
        public int EventVersion { get; init; } = CurrentEventVersion;

        /// <summary>
        /// Wraps this payload as an event of the given type.
        /// </summary>
        /// <param name="type">One of <see cref="ApplicationEventTypes"/>.</param>
        /// <returns>The domain event.</returns>
        public DomainEvent ToEvent(string type)
        {
            return new DomainEvent(type, JsonSerializer.SerializeToElement(this));
        }
    }

    /// <summary>
    /// Payload of a <see cref="ApplicationEventTypes.Submitted"/> or
    /// <see cref="ApplicationEventTypes.Resubmitted"/> event. It carries a
    /// copy of the answers, so what was submitted lives in the log regardless
    /// of what happens to the working copy afterwards.
    /// </summary>
    public sealed class SubmittedPayload
    {
        /// <summary>The payload shape version, for upcasting later.</summary>
        public const int CurrentEventVersion = 1;

        /// <summary>
        /// Gets the payload shape version.
        /// </summary>
        [JsonPropertyName("eventVersion")]
        public int EventVersion { get; init; } = CurrentEventVersion;

        /// <summary>
        /// Gets the spec version the answers were rendered and validated against.
        /// </summary>
        [JsonPropertyName("formSpecVersion")]
        public required int FormSpecVersion { get; init; }

        /// <summary>
        /// Gets the submitted answers.
        /// </summary>
        [JsonPropertyName("answers")]
        public required JsonElement Answers { get; init; }

        /// <summary>
        /// Wraps this payload as a first submission.
        /// </summary>
        /// <returns>The domain event.</returns>
        public DomainEvent ToEvent()
        {
            return ToEvent(ApplicationEventTypes.Submitted);
        }

        /// <summary>
        /// Wraps this payload as an event of the given type.
        /// </summary>
        /// <param name="type"><see cref="ApplicationEventTypes.Submitted"/> or <see cref="ApplicationEventTypes.Resubmitted"/>.</param>
        /// <returns>The domain event.</returns>
        public DomainEvent ToEvent(string type)
        {
            return new DomainEvent(type, JsonSerializer.SerializeToElement(this));
        }

        /// <summary>
        /// Reads a stored payload back.
        /// </summary>
        /// <param name="payload">The stored payload.</param>
        /// <returns>The typed payload.</returns>
        public static SubmittedPayload From(JsonElement payload)
        {
            return payload.Deserialize<SubmittedPayload>()
                ?? throw new JsonException("A Submitted event has no payload.");
        }
    }

    /// <summary>
    /// Payload of a <see cref="ApplicationEventTypes.NoteAdded"/> event: the
    /// note's text. Internal to workers. It may hold personal information, so
    /// it is never logged.
    /// </summary>
    public sealed class NotePayload
    {
        /// <summary>The payload shape version, for upcasting later.</summary>
        public const int CurrentEventVersion = 1;

        /// <summary>The longest note accepted.</summary>
        public const int MaxLength = 4000;

        /// <summary>
        /// Gets the payload shape version.
        /// </summary>
        [JsonPropertyName("eventVersion")]
        public int EventVersion { get; init; } = CurrentEventVersion;

        /// <summary>
        /// Gets the note's text.
        /// </summary>
        [JsonPropertyName("text")]
        public required string Text { get; init; }

        /// <summary>
        /// Wraps this payload as an event for the store.
        /// </summary>
        /// <returns>The domain event.</returns>
        public DomainEvent ToEvent()
        {
            return new DomainEvent(ApplicationEventTypes.NoteAdded, JsonSerializer.SerializeToElement(this));
        }

        /// <summary>
        /// Reads a stored payload back.
        /// </summary>
        /// <param name="payload">The stored payload.</param>
        /// <returns>The typed payload.</returns>
        public static NotePayload From(JsonElement payload)
        {
            return payload.Deserialize<NotePayload>()
                ?? throw new JsonException("A NoteAdded event has no payload.");
        }
    }

    /// <summary>
    /// Payload of a <see cref="ApplicationEventTypes.PromotionFailed"/>
    /// event: a short reason for the operator, never applicant data.
    /// </summary>
    public sealed class PromotionFailedPayload
    {
        /// <summary>The payload shape version, for upcasting later.</summary>
        public const int CurrentEventVersion = 1;

        /// <summary>
        /// Gets the payload shape version.
        /// </summary>
        [JsonPropertyName("eventVersion")]
        public int EventVersion { get; init; } = CurrentEventVersion;

        /// <summary>
        /// Gets why the promotion failed.
        /// </summary>
        [JsonPropertyName("reason")]
        public required string Reason { get; init; }

        /// <summary>
        /// Wraps this payload as an event for the store.
        /// </summary>
        /// <returns>The domain event.</returns>
        public DomainEvent ToEvent()
        {
            return new DomainEvent(ApplicationEventTypes.PromotionFailed, JsonSerializer.SerializeToElement(this));
        }
    }
}
