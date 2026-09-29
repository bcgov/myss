namespace Myss.Api.Intake
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Myss.Api.Platform;

    /// <summary>
    /// The intake module's event types. Only <see cref="Submitted"/> exists
    /// in the applicant slice; the review workflow adds the rest.
    /// </summary>
    public static class ApplicationEventTypes
    {
        /// <summary>The applicant submitted the application.</summary>
        public const string Submitted = "Submitted";
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
