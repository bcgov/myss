namespace Myss.Api.Intake
{
    using System;
    using System.Text.Json;
    using Myss.Api.Platform;

    /// <summary>
    /// Where an application is in its lifecycle. Computed, never stored.
    /// </summary>
    public enum ApplicationStatus
    {
        /// <summary>Being drafted: the stream is empty.</summary>
        Draft,

        /// <summary>Submitted and read-only for the applicant.</summary>
        Submitted,
    }

    /// <summary>
    /// The folded state of an application.
    /// </summary>
    /// <param name="Status">The lifecycle status.</param>
    /// <param name="Version">The stream version the state was folded from.</param>
    /// <param name="SubmittedAt">When it was submitted, if it was.</param>
    /// <param name="Submitted">The Submitted payload, if it was.</param>
    public sealed record ApplicationState(
        ApplicationStatus Status,
        int Version,
        DateTimeOffset? SubmittedAt,
        SubmittedPayload? Submitted)
    {
        /// <summary>
        /// Gets the state of an empty stream.
        /// </summary>
        public static ApplicationState Seed { get; } = new(ApplicationStatus.Draft, 0, null, null);

        /// <summary>
        /// Gets the API status code for this state.
        /// </summary>
        public string StatusCode => Status switch
        {
            ApplicationStatus.Draft => ApplicationStatusCodes.Draft,
            ApplicationStatus.Submitted => ApplicationStatusCodes.Submitted,
            _ => throw new InvalidOperationException($"Unmapped status {Status}."),
        };
    }

    /// <summary>
    /// The status codes the API exposes. The display text is the client's
    /// (interim) and later Strapi's; these codes are the contract.
    /// </summary>
    public static class ApplicationStatusCodes
    {
        /// <summary>Being drafted.</summary>
        public const string Draft = "DRAFT";

        /// <summary>Submitted.</summary>
        public const string Submitted = "SUBMITTED";
    }

    /// <summary>
    /// The fold and the rules: what an application's events say it is, and
    /// what may happen to it next. Both are pure so they can be table-tested.
    /// </summary>
    public static class ApplicationProjection
    {
        /// <summary>
        /// Folds a stream into its state.
        /// </summary>
        /// <param name="stream">The application's events.</param>
        /// <returns>The state.</returns>
        public static ApplicationState Fold(EventStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return stream.Project(ApplicationState.Seed, Step);
        }

        /// <summary>
        /// One fold step.
        /// </summary>
        /// <param name="state">The state so far.</param>
        /// <param name="evt">The next event.</param>
        /// <returns>The new state.</returns>
        public static ApplicationState Step(ApplicationState state, StoredEvent evt)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(evt);
            return evt.Type switch
            {
                ApplicationEventTypes.Submitted => state with
                {
                    Status = ApplicationStatus.Submitted,
                    Version = evt.Version,
                    SubmittedAt = evt.OccurredAt,
                    Submitted = SubmittedPayload.From(evt.Payload),
                },

                // An event this slice does not know (the review workflow's, once
                // it lands) leaves the applicant-facing state untouched.
                _ => state with { Version = evt.Version },
            };
        }

        /// <summary>
        /// Whether the applicant may save answers.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>True while drafting.</returns>
        public static bool CanSave(ApplicationState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status == ApplicationStatus.Draft;
        }

        /// <summary>
        /// Whether the applicant may submit.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>True while drafting.</returns>
        public static bool CanSubmit(ApplicationState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status == ApplicationStatus.Draft;
        }
    }
}
