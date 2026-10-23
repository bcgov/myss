namespace Myss.Data.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Myss.Data.Platform;

    /// <summary>
    /// Where an application is in its lifecycle. Computed, never stored.
    /// </summary>
    public enum ApplicationStatus
    {
        /// <summary>Being drafted: the stream is empty.</summary>
        Draft,

        /// <summary>Submitted and read-only for the applicant; waiting in the queue.</summary>
        Submitted,

        /// <summary>Picked up by a worker, who is the only one who can act on it.</summary>
        UnderReview,

        /// <summary>A worker asked for more information; the applicant can edit and resubmit.</summary>
        Returned,

        /// <summary>The applicant submitted a returned application again; back in the queue.</summary>
        Resubmitted,

        /// <summary>Escalated by a worker for a further review. No worker action in this slice.</summary>
        Escalated,

        /// <summary>Accepted by a worker.</summary>
        Accepted,

        /// <summary>Denied by a worker. Terminal.</summary>
        Denied,

        /// <summary>Accepted, but handing it on to the case management system failed.</summary>
        PromotionFailed,
    }

    /// <summary>
    /// The moves a worker can make. Which are available is computed from
    /// state and the caller by <see cref="ApplicationProjection.AvailableWorkerActions"/>.
    /// </summary>
    public static class WorkerActions
    {
        /// <summary>Pick the application up from the queue; it goes Under Review, assigned to the caller.</summary>
        public const string Review = "Review";

        /// <summary>Put it back in the queue without deciding it.</summary>
        public const string Release = "Release";

        /// <summary>Return it to the applicant for more information.</summary>
        public const string RequestMoreInfo = "RequestMoreInfo";

        /// <summary>Escalate it for a further review.</summary>
        public const string Escalate = "Escalate";

        /// <summary>Accept it.</summary>
        public const string Accept = "Accept";

        /// <summary>Deny it.</summary>
        public const string Deny = "Deny";
    }

    /// <summary>
    /// The codes a history entry carries: what happened to the file, in the
    /// API's words. The decision codes are the ones the review story names.
    /// </summary>
    public static class ApplicationHistoryCodes
    {
        /// <summary>The applicant submitted.</summary>
        public const string Submit = "SUBMIT";

        /// <summary>The applicant resubmitted after a return.</summary>
        public const string Resubmit = "RESUBMIT";

        /// <summary>A worker picked it up.</summary>
        public const string PickUp = "PICK_UP";

        /// <summary>The assignee put it back in the queue.</summary>
        public const string Release = "RELEASE";

        /// <summary>The assignee asked for more information.</summary>
        public const string RequestMoreInfo = "REQUEST_MORE_INFO";

        /// <summary>The assignee escalated it.</summary>
        public const string Escalate = "ESCALATE";

        /// <summary>The assignee accepted it.</summary>
        public const string Accept = "ACCEPT";

        /// <summary>The assignee denied it.</summary>
        public const string Deny = "DENY";

        /// <summary>It was handed on to the case management system.</summary>
        public const string Promoted = "PROMOTED";

        /// <summary>Handing it on failed.</summary>
        public const string PromotionFailed = "PROMOTION_FAILED";
    }

    /// <summary>
    /// One thing that happened to an application, as the worker's history shows it.
    /// </summary>
    /// <param name="Code">One of <see cref="ApplicationHistoryCodes"/>.</param>
    /// <param name="Actor">The event's actor string.</param>
    /// <param name="OccurredAt">When it happened.</param>
    public sealed record ApplicationHistoryEntry(string Code, string Actor, DateTimeOffset OccurredAt);

    /// <summary>
    /// One internal note.
    /// </summary>
    /// <param name="Text">What the worker wrote.</param>
    /// <param name="AuthorIdir">The worker who wrote it.</param>
    /// <param name="CreatedAt">When.</param>
    public sealed record ApplicationNote(string Text, string AuthorIdir, DateTimeOffset CreatedAt);

    /// <summary>
    /// The folded state of an application.
    /// </summary>
    /// <param name="Status">The lifecycle status.</param>
    /// <param name="Version">The stream version the state was folded from.</param>
    /// <param name="SubmittedAt">When it was first submitted, if it was.</param>
    /// <param name="Submitted">The latest Submitted or Resubmitted payload, if there is one.</param>
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
        /// Gets the IDIR of the worker the application is assigned to. Set
        /// only while it is Under Review.
        /// </summary>
        public string? Assignee { get; init; }

        /// <summary>
        /// Gets the status it returns to if the assignee releases it:
        /// Submitted, or Resubmitted after a return.
        /// </summary>
        public ApplicationStatus QueueStatus { get; init; } = ApplicationStatus.Submitted;

        /// <summary>
        /// Gets when it was last returned to the applicant, if it ever was.
        /// </summary>
        public DateTimeOffset? ReturnedAt { get; init; }

        /// <summary>
        /// Gets the internal notes, oldest first.
        /// </summary>
        public ImmutableList<ApplicationNote> Notes { get; init; } = [];

        /// <summary>
        /// Gets what happened to the file, oldest first. Notes are not in it.
        /// </summary>
        public ImmutableList<ApplicationHistoryEntry> History { get; init; } = [];

        /// <summary>
        /// Gets the API status code for this state.
        /// </summary>
        public string StatusCode => Status switch
        {
            ApplicationStatus.Draft => ApplicationStatusCodes.Draft,
            ApplicationStatus.Submitted => ApplicationStatusCodes.Submitted,
            ApplicationStatus.UnderReview => ApplicationStatusCodes.UnderReview,
            ApplicationStatus.Returned => ApplicationStatusCodes.Returned,
            ApplicationStatus.Resubmitted => ApplicationStatusCodes.Resubmitted,
            ApplicationStatus.Escalated => ApplicationStatusCodes.Escalated,
            ApplicationStatus.Accepted => ApplicationStatusCodes.Accepted,
            ApplicationStatus.Denied => ApplicationStatusCodes.Denied,
            ApplicationStatus.PromotionFailed => ApplicationStatusCodes.PromotionFailed,
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

        /// <summary>Under review by the assigned worker.</summary>
        public const string UnderReview = "UNDER_REVIEW";

        /// <summary>Returned to the applicant for more information.</summary>
        public const string Returned = "RETURNED";

        /// <summary>Submitted again after a return.</summary>
        public const string Resubmitted = "RESUBMITTED";

        /// <summary>Escalated for a further review.</summary>
        public const string Escalated = "ESCALATED";

        /// <summary>Accepted.</summary>
        public const string Accepted = "ACCEPTED";

        /// <summary>Denied.</summary>
        public const string Denied = "DENIED";

        /// <summary>Accepted, but the hand-off to the case management system failed.</summary>
        public const string PromotionFailed = "PROMOTION_FAILED";
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
            ApplicationState next = evt.Type switch
            {
                ApplicationEventTypes.Submitted => Logged(state, evt, ApplicationHistoryCodes.Submit) with
                {
                    Status = ApplicationStatus.Submitted,
                    QueueStatus = ApplicationStatus.Submitted,
                    SubmittedAt = evt.OccurredAt,
                    Submitted = SubmittedPayload.From(evt.Payload),
                },
                ApplicationEventTypes.Resubmitted => Logged(state, evt, ApplicationHistoryCodes.Resubmit) with
                {
                    Status = ApplicationStatus.Resubmitted,
                    QueueStatus = ApplicationStatus.Resubmitted,
                    Submitted = SubmittedPayload.From(evt.Payload),
                },

                // ReviewStarted is the first slice's marker: same meaning, the
                // actor becomes the assignee.
                ApplicationEventTypes.PickedUp or ApplicationEventTypes.ReviewStarted =>
                    Logged(state, evt, ApplicationHistoryCodes.PickUp) with
                    {
                        Status = ApplicationStatus.UnderReview,
                        Assignee = ApplicationActors.IdirOf(evt.Actor),
                    },
                ApplicationEventTypes.Released => Logged(state, evt, ApplicationHistoryCodes.Release) with
                {
                    Status = state.QueueStatus,
                    Assignee = null,
                },
                ApplicationEventTypes.NoteAdded => state with
                {
                    Notes = state.Notes.Add(new ApplicationNote(
                        NotePayload.From(evt.Payload).Text,
                        ApplicationActors.IdirOf(evt.Actor) ?? evt.Actor,
                        evt.OccurredAt)),
                },
                ApplicationEventTypes.MoreInfoRequested => Logged(state, evt, ApplicationHistoryCodes.RequestMoreInfo) with
                {
                    Status = ApplicationStatus.Returned,
                    Assignee = null,
                    ReturnedAt = evt.OccurredAt,
                },
                ApplicationEventTypes.Escalated => Logged(state, evt, ApplicationHistoryCodes.Escalate) with
                {
                    Status = ApplicationStatus.Escalated,
                    Assignee = null,
                },
                ApplicationEventTypes.Accepted => Logged(state, evt, ApplicationHistoryCodes.Accept) with
                {
                    Status = ApplicationStatus.Accepted,
                    Assignee = null,
                },
                ApplicationEventTypes.Denied => Logged(state, evt, ApplicationHistoryCodes.Deny) with
                {
                    Status = ApplicationStatus.Denied,
                    Assignee = null,
                },
                ApplicationEventTypes.Promoted => Logged(state, evt, ApplicationHistoryCodes.Promoted),
                ApplicationEventTypes.PromotionFailed => Logged(state, evt, ApplicationHistoryCodes.PromotionFailed) with
                {
                    Status = ApplicationStatus.PromotionFailed,
                },

                // An event type this fold does not know leaves the state untouched.
                _ => state,
            };
            return next with { Version = evt.Version };
        }

        /// <summary>
        /// Whether the applicant may save answers.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>True while drafting, or once returned for more information.</returns>
        public static bool CanSave(ApplicationState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status is ApplicationStatus.Draft or ApplicationStatus.Returned;
        }

        /// <summary>
        /// Whether the applicant may submit.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>True while drafting, or once returned for more information.</returns>
        public static bool CanSubmit(ApplicationState state)
        {
            return CanSave(state);
        }

        /// <summary>
        /// The event a submit appends from this state: the first submission,
        /// or a resubmission after a return.
        /// </summary>
        /// <param name="state">A state <see cref="CanSubmit"/> allows.</param>
        /// <returns>The event type.</returns>
        public static string SubmitEventFor(ApplicationState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status == ApplicationStatus.Returned
                ? ApplicationEventTypes.Resubmitted
                : ApplicationEventTypes.Submitted;
        }

        /// <summary>
        /// Whether a worker's queue holds the application: waiting to be
        /// picked up, or waiting on the applicant after a return.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>True for Submitted, Resubmitted and Returned.</returns>
        public static bool IsInQueue(ApplicationState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status is ApplicationStatus.Submitted or ApplicationStatus.Resubmitted or ApplicationStatus.Returned;
        }

        /// <summary>
        /// Whether the application is assigned to someone other than the caller.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <param name="idir">The caller's IDIR username.</param>
        /// <returns>True while Under Review with another assignee.</returns>
        public static bool IsAssignedToAnother(ApplicationState state, string idir)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status == ApplicationStatus.UnderReview && !IsAssignee(state, idir);
        }

        /// <summary>
        /// The moves a worker may make from this state. Anyone may pick up a
        /// file waiting in the queue; once it is Under Review only the
        /// assignee may act on it.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <param name="idir">The caller's IDIR username.</param>
        /// <returns>The available <see cref="WorkerActions"/>, possibly none.</returns>
        public static IReadOnlyList<string> AvailableWorkerActions(ApplicationState state, string idir)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status switch
            {
                ApplicationStatus.Submitted or ApplicationStatus.Resubmitted => [WorkerActions.Review],
                ApplicationStatus.UnderReview when IsAssignee(state, idir) =>
                [
                    WorkerActions.RequestMoreInfo,
                    WorkerActions.Escalate,
                    WorkerActions.Deny,
                    WorkerActions.Accept,
                    WorkerActions.Release,
                ],
                _ => [],
            };
        }

        /// <summary>
        /// Whether the caller may add an internal note: the assignee, while
        /// the file is Under Review.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <param name="idir">The caller's IDIR username.</param>
        /// <returns>True for the assignee of a file Under Review.</returns>
        public static bool CanAddNote(ApplicationState state, string idir)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.Status == ApplicationStatus.UnderReview && IsAssignee(state, idir);
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
                WorkerActions.Review => ApplicationEventTypes.PickedUp,
                WorkerActions.Release => ApplicationEventTypes.Released,
                WorkerActions.RequestMoreInfo => ApplicationEventTypes.MoreInfoRequested,
                WorkerActions.Escalate => ApplicationEventTypes.Escalated,
                WorkerActions.Accept => ApplicationEventTypes.Accepted,
                WorkerActions.Deny => ApplicationEventTypes.Denied,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown worker action."),
            };
        }

        private static bool IsAssignee(ApplicationState state, string idir)
        {
            return state.Assignee is not null && string.Equals(state.Assignee, idir, StringComparison.OrdinalIgnoreCase);
        }

        private static ApplicationState Logged(ApplicationState state, StoredEvent evt, string code)
        {
            return state with { History = state.History.Add(new ApplicationHistoryEntry(code, evt.Actor, evt.OccurredAt)) };
        }
    }
}
