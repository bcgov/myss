namespace Myss.AdminApi.Intake
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Text.Json;
    using Myss.AdminApi.Models;
    using Myss.Data.Intake;
    using Myss.FormSpec;

    /// <summary>
    /// An application as a worker's list shows it.
    /// </summary>
    public class ReviewApplicationSummaryModel
    {
        /// <summary>
        /// Gets or sets the application identifier.
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the request number, the same one the applicant was shown.
        /// </summary>
        public required string ReferenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the status code (<see cref="ApplicationStatusCodes"/>).
        /// </summary>
        public required string Status { get; set; }

        /// <summary>
        /// Gets or sets when the application was submitted.
        /// </summary>
        public required DateTimeOffset SubmittedAt { get; set; }

        /// <summary>
        /// Gets or sets the stream version a worker must send back with an
        /// action. Worker actions are events, so this is the event log's
        /// version, not the applicant's row version.
        /// </summary>
        public required int StreamVersion { get; set; }

        /// <summary>
        /// Gets or sets the IDIR of the worker it is assigned to, while it is
        /// Under Review; null otherwise.
        /// </summary>
        public string? Assignee { get; set; }

        /// <summary>
        /// Gets or sets whether the application is in the worker queue:
        /// Submitted, Re-submitted or Returned.
        /// </summary>
        public required bool InQueue { get; set; }
    }

    /// <summary>
    /// An application as a worker reads it: the summary plus what the applicant
    /// submitted, the spec that renders it, and the actions open right now.
    /// </summary>
    public class ReviewApplicationModel : ReviewApplicationSummaryModel
    {
        /// <summary>
        /// Gets or sets the submitted answers, from the Submitted event.
        /// </summary>
        public required JsonElement Answers { get; set; }

        /// <summary>
        /// Gets or sets the logical form identifier.
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the pinned spec version.
        /// </summary>
        public required int FormSpecVersion { get; set; }

        /// <summary>
        /// Gets or sets the archived spec at the pinned version. Null when the
        /// content engine no longer has it, and on action responses.
        /// </summary>
        public FormSpecModel? Spec { get; set; }

        /// <summary>
        /// Gets or sets the worker actions available in the current state
        /// (<see cref="WorkerActions"/>). The buttons render from this list.
        /// </summary>
        public required IReadOnlyList<string> AvailableActions { get; set; }

        /// <summary>
        /// Gets or sets whether the caller may add an internal note right now:
        /// true for the assignee of a file Under Review.
        /// </summary>
        public required bool CanAddNote { get; set; }

        /// <summary>
        /// Gets or sets the internal notes, oldest first. Workers only.
        /// </summary>
        public required IReadOnlyList<ReviewNoteModel> Notes { get; set; }

        /// <summary>
        /// Gets or sets what happened to the file, oldest first.
        /// </summary>
        public required IReadOnlyList<ReviewHistoryEntryModel> History { get; set; }
    }

    /// <summary>
    /// One internal note on an application.
    /// </summary>
    public class ReviewNoteModel
    {
        /// <summary>
        /// Gets or sets what the worker wrote.
        /// </summary>
        public required string Text { get; set; }

        /// <summary>
        /// Gets or sets the IDIR of the worker who wrote it.
        /// </summary>
        public required string Author { get; set; }

        /// <summary>
        /// Gets or sets when it was written.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// One entry of an application's history.
    /// </summary>
    public class ReviewHistoryEntryModel
    {
        /// <summary>The actor kind of an applicant's event.</summary>
        public const string ApplicantActor = "APPLICANT";

        /// <summary>The actor kind of a worker's event.</summary>
        public const string WorkerActor = "WORKER";

        /// <summary>The actor kind of an event the API appended itself.</summary>
        public const string SystemActor = "SYSTEM";

        /// <summary>
        /// Gets or sets what happened (<see cref="ApplicationHistoryCodes"/>).
        /// </summary>
        public required string Code { get; set; }

        /// <summary>
        /// Gets or sets who did it: APPLICANT, WORKER or SYSTEM.
        /// </summary>
        public required string ActorKind { get; set; }

        /// <summary>
        /// Gets or sets the worker's IDIR, when a worker did it.
        /// </summary>
        public string? Idir { get; set; }

        /// <summary>
        /// Gets or sets when it happened.
        /// </summary>
        public required DateTimeOffset OccurredAt { get; set; }
    }

    /// <summary>
    /// The body of a worker action: the stream version the worker last saw.
    /// </summary>
    public class ReviewActionRequestModel
    {
        /// <summary>
        /// Gets or sets the stream version the worker last saw.
        /// </summary>
        public required int StreamVersion { get; set; }
    }

    /// <summary>
    /// The body of an internal note: the stream version the worker last saw
    /// and the text.
    /// </summary>
    public class ReviewNoteRequestModel
    {
        /// <summary>
        /// Gets or sets the stream version the worker last saw.
        /// </summary>
        public required int StreamVersion { get; set; }

        /// <summary>
        /// Gets or sets the note's text.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [StringLength(NotePayload.MaxLength)]
        public required string Text { get; set; }
    }

    /// <summary>
    /// How a review operation ended.
    /// </summary>
    public enum ReviewOutcome
    {
        /// <summary>The operation succeeded; <see cref="ReviewResultModel.Application"/> is set.</summary>
        Ok,

        /// <summary>No submitted application with that id exists.</summary>
        NotFound,

        /// <summary>The action is not available in the application's current state.</summary>
        NotAllowed,

        /// <summary>The worker's stream version is behind; another action landed first.</summary>
        StaleVersion,

        /// <summary>The application is Under Review by another worker; <see cref="ReviewResultModel.Assignee"/> is set.</summary>
        AssignedToAnother,
    }

    /// <summary>
    /// The result of a review operation, one shape for every outcome.
    /// </summary>
    public sealed class ReviewResultModel
    {
        private ReviewResultModel(ReviewOutcome outcome)
        {
            Outcome = outcome;
        }

        /// <summary>
        /// Gets the outcome.
        /// </summary>
        public ReviewOutcome Outcome { get; }

        /// <summary>
        /// Gets the application, on success.
        /// </summary>
        public ReviewApplicationModel? Application { get; private init; }

        /// <summary>
        /// Gets the stream version the server holds, on a refusal, so the
        /// worker's view can reload.
        /// </summary>
        public int? CurrentVersion { get; private init; }

        /// <summary>
        /// Gets the IDIR of the worker the application is assigned to, when
        /// the caller is not them.
        /// </summary>
        public string? Assignee { get; private init; }

        /// <summary>Builds a success result.</summary>
        /// <param name="application">The application.</param>
        /// <returns>The result.</returns>
        public static ReviewResultModel Ok(ReviewApplicationModel application)
        {
            return new(ReviewOutcome.Ok) { Application = application };
        }

        /// <summary>Builds a not-found result.</summary>
        /// <returns>The result.</returns>
        public static ReviewResultModel NotFound()
        {
            return new(ReviewOutcome.NotFound);
        }

        /// <summary>Builds a not-allowed result.</summary>
        /// <param name="currentVersion">The stream version the server holds.</param>
        /// <returns>The result.</returns>
        public static ReviewResultModel NotAllowed(int currentVersion)
        {
            return new(ReviewOutcome.NotAllowed) { CurrentVersion = currentVersion };
        }

        /// <summary>Builds a stale-version result.</summary>
        /// <param name="currentVersion">The stream version the server holds, when known.</param>
        /// <returns>The result.</returns>
        public static ReviewResultModel StaleVersion(int? currentVersion)
        {
            return new(ReviewOutcome.StaleVersion) { CurrentVersion = currentVersion };
        }

        /// <summary>Builds an assigned-to-another result.</summary>
        /// <param name="currentVersion">The stream version the server holds.</param>
        /// <param name="assignee">The IDIR of the worker who holds it.</param>
        /// <returns>The result.</returns>
        public static ReviewResultModel AssignedToAnother(int currentVersion, string? assignee)
        {
            return new(ReviewOutcome.AssignedToAnother) { CurrentVersion = currentVersion, Assignee = assignee };
        }
    }
}
