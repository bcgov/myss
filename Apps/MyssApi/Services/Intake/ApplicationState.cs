namespace Myss.Api.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Myss.Api.Platform;

    /// <summary>
    /// Where an application is in its lifecycle. Computed, never stored.
    /// </summary>
    public enum ApplicationStatus
    {
        /// <summary>Being drafted: the stream is empty.</summary>
        Draft,

        /// <summary>Submitted and read-only for the applicant; waiting for a worker.</summary>
        Submitted,

        /// <summary>A worker has marked it Under Review.</summary>
        UnderReview,

        /// <summary>Accepted by a worker. Terminal in this slice.</summary>
        Accepted,

        /// <summary>Denied by a worker. Terminal.</summary>
        Denied,
    }

    /// <summary>
    /// The moves a worker can make. Which are available is computed from
    /// state by <see cref="ApplicationProjection.AvailableWorkerActions"/>.
    /// </summary>
    public static class WorkerActions
    {
        /// <summary>Mark the application Under Review.</summary>
        public const string Review = "Review";

        /// <summary>Accept it.</summary>
        public const string Accept = "Accept";

        /// <summary>Deny it.</summary>
        public const string Deny = "Deny";
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
            ApplicationStatus.UnderReview => ApplicationStatusCodes.UnderReview,
            ApplicationStatus.Accepted => ApplicationStatusCodes.Accepted,
            ApplicationStatus.Denied => ApplicationStatusCodes.Denied,
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

        /// <summary>Under review by a worker.</summary>
        public const string UnderReview = "UNDER_REVIEW";

        /// <summary>Accepted.</summary>
        public const string Accepted = "ACCEPTED";

        /// <summary>Denied.</summary>
        public const string Denied = "DENIED";
    }

    /// <summary>
    /// The request number shown to the applicant at submission and to workers
    /// in their list: derived from the application id in this one place, so
    /// both sides always show the same string. No column and no sequence;
    /// unique in practice at POC scale rather than by guarantee. A database
    /// sequence can replace this without changing the API.
    /// </summary>
    public static class ApplicationReference
    {
        /// <summary>The prefix every reference carries.</summary>
        public const string Prefix = "IA-";

        /// <summary>
        /// Derives the reference for an application.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <returns>"IA-" followed by the first eight hex characters of the id, upper case.</returns>
        public static string From(Guid applicationId)
        {
            return Prefix + applicationId.ToString("N")[..8].ToUpperInvariant();
        }
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
                ApplicationEventTypes.ReviewStarted => state with
                {
                    Status = ApplicationStatus.UnderReview,
                    Version = evt.Version,
                },
                ApplicationEventTypes.Accepted => state with
                {
                    Status = ApplicationStatus.Accepted,
                    Version = evt.Version,
                },
                ApplicationEventTypes.Denied => state with
                {
                    Status = ApplicationStatus.Denied,
                    Version = evt.Version,
                },

                // An event this slice does not know (assignment, notes, Request
                // More Info, once they land) leaves the state untouched.
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

        /// <summary>
        /// The moves a worker may make from this state. Any worker; there is
        /// no assignment in this slice. Under Review comes before a decision.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>The available <see cref="WorkerActions"/>, possibly none.</returns>
        public static IReadOnlyList<string> AvailableWorkerActions(ApplicationState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status switch
            {
                ApplicationStatus.Submitted => [WorkerActions.Review],
                ApplicationStatus.UnderReview => [WorkerActions.Accept, WorkerActions.Deny],
                _ => [],
            };
        }

        /// <summary>
        /// The event a worker action appends.
        /// </summary>
        /// <param name="action">One of <see cref="WorkerActions"/>.</param>
        /// <returns>The event type.</returns>
        public static string EventFor(string action)
        {
            return action switch
            {
                WorkerActions.Review => ApplicationEventTypes.ReviewStarted,
                WorkerActions.Accept => ApplicationEventTypes.Accepted,
                WorkerActions.Deny => ApplicationEventTypes.Denied,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown worker action."),
            };
        }
    }
}
