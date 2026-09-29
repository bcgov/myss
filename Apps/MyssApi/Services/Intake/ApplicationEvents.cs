namespace Myss.Api.Intake
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Myss.Api.Platform;

    /// <summary>
    /// The intake module's event types: the applicant's submit and the worker
    /// status moves of the review slice. Assignment, notes, Request More Info
    /// and Escalate are not modelled yet.
    /// </summary>
    public static class ApplicationEventTypes
    {
        /// <summary>The applicant submitted the application.</summary>
        public const string Submitted = "Submitted";

        /// <summary>A worker marked the application Under Review. Records who, not ownership.</summary>
        public const string ReviewStarted = "ReviewStarted";

        /// <summary>A worker accepted the application. Terminal in this slice.</summary>
        public const string Accepted = "Accepted";

        /// <summary>A worker denied the application. Terminal.</summary>
        public const string Denied = "Denied";
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
    /// Payload of a <see cref="ApplicationEventTypes.Submitted"/> event. It
    /// carries a copy of the answers, so what was submitted lives in the log
    /// regardless of what happens to the working copy afterwards.
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
        /// Wraps this payload as an event for the store.
        /// </summary>
        /// <returns>The domain event.</returns>
        public DomainEvent ToEvent()
        {
            return new DomainEvent(ApplicationEventTypes.Submitted, JsonSerializer.SerializeToElement(this));
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
}
