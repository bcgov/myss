namespace Myss.Api.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Myss.Api.Models;

    /// <summary>
    /// An application as the list endpoint returns it: identity, status and dates.
    /// </summary>
    public class ApplicationSummaryModel
    {
        /// <summary>
        /// Gets or sets the application identifier.
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the request number shown to the applicant and to
        /// workers (<see cref="ApplicationReference"/>).
        /// </summary>
        public required string ReferenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the status code (<see cref="ApplicationStatusCodes"/>).
        /// </summary>
        public required string Status { get; set; }

        /// <summary>
        /// Gets or sets the row version the client must send back on a write.
        /// </summary>
        public required int Version { get; set; }

        /// <summary>
        /// Gets or sets the logical form identifier.
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the pinned spec version.
        /// </summary>
        public required int FormSpecVersion { get; set; }

        /// <summary>
        /// Gets or sets when the application was created.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the answers were last saved.
        /// </summary>
        public required DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the application was submitted, if it was.
        /// </summary>
        public DateTimeOffset? SubmittedAt { get; set; }
    }

    /// <summary>
    /// An application in full: the summary plus its answers and, on a single
    /// read, the archived spec version that renders them.
    /// </summary>
    public class ApplicationModel : ApplicationSummaryModel
    {
        /// <summary>
        /// Gets or sets the answers: the working copy while drafting, the
        /// submitted copy afterwards.
        /// </summary>
        public required JsonElement Answers { get; set; }

        /// <summary>
        /// Gets or sets the archived spec at the pinned version. Null when the
        /// content engine no longer has it or on write responses.
        /// </summary>
        public FormSpecModel? Spec { get; set; }
    }

    /// <summary>
    /// The body of a save or a submit: the row version the client last saw and
    /// the answers.
    /// </summary>
    public class ApplicationAnswersRequestModel
    {
        /// <summary>
        /// Gets or sets the row version the client last saw.
        /// </summary>
        public required int Version { get; set; }

        /// <summary>
        /// Gets or sets the answers.
        /// </summary>
        public required JsonElement Answers { get; set; }
    }

    /// <summary>
    /// How an intake operation ended.
    /// </summary>
    public enum IntakeOutcome
    {
        /// <summary>The operation succeeded; <see cref="IntakeResultModel.Application"/> is set.</summary>
        Ok,

        /// <summary>No application with that id belongs to the caller.</summary>
        NotFound,

        /// <summary>The caller has no registered profile.</summary>
        ProfileRequired,

        /// <summary>The application's state does not allow the change.</summary>
        NotEditable,

        /// <summary>The caller's row version is behind; <see cref="IntakeResultModel.CurrentVersion"/> is set when known.</summary>
        StaleVersion,

        /// <summary>The event append lost a race; the caller reloads.</summary>
        StreamConflict,

        /// <summary>The answers failed validation; <see cref="IntakeResultModel.Errors"/> is set.</summary>
        Invalid,

        /// <summary>The content engine could not supply the spec.</summary>
        SpecUnavailable,
    }

    /// <summary>
    /// The result of an intake operation. One shape for every outcome so the
    /// controller maps it to HTTP in one place.
    /// </summary>
    public sealed class IntakeResultModel
    {
        private IntakeResultModel(IntakeOutcome outcome)
        {
            Outcome = outcome;
        }

        /// <summary>
        /// Gets the outcome.
        /// </summary>
        public IntakeOutcome Outcome { get; }

        /// <summary>
        /// Gets the application, on success.
        /// </summary>
        public ApplicationModel? Application { get; private init; }

        /// <summary>
        /// Gets the validation errors, when invalid.
        /// </summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; private init; } = [];

        /// <summary>
        /// Gets the row version the server holds, on a stale-version or
        /// not-editable outcome, so the client can refetch.
        /// </summary>
        public int? CurrentVersion { get; private init; }

        /// <summary>Builds a success result.</summary>
        /// <param name="application">The application.</param>
        /// <returns>The result.</returns>
        public static IntakeResultModel Ok(ApplicationModel application)
        {
            return new(IntakeOutcome.Ok) { Application = application };
        }

        /// <summary>Builds a not-found result.</summary>
        /// <returns>The result.</returns>
        public static IntakeResultModel NotFound()
        {
            return new(IntakeOutcome.NotFound);
        }

        /// <summary>Builds a profile-required result.</summary>
        /// <returns>The result.</returns>
        public static IntakeResultModel ProfileRequired()
        {
            return new(IntakeOutcome.ProfileRequired);
        }

        /// <summary>Builds a not-editable result.</summary>
        /// <param name="currentVersion">The row version the server holds.</param>
        /// <returns>The result.</returns>
        public static IntakeResultModel NotEditable(int currentVersion)
        {
            return new(IntakeOutcome.NotEditable) { CurrentVersion = currentVersion };
        }

        /// <summary>Builds a stale-version result.</summary>
        /// <param name="currentVersion">The row version the server holds, when known.</param>
        /// <returns>The result.</returns>
        public static IntakeResultModel StaleVersion(int? currentVersion)
        {
            return new(IntakeOutcome.StaleVersion) { CurrentVersion = currentVersion };
        }

        /// <summary>Builds a stream-conflict result.</summary>
        /// <param name="currentVersion">The row version the server holds.</param>
        /// <returns>The result.</returns>
        public static IntakeResultModel StreamConflict(int currentVersion)
        {
            return new(IntakeOutcome.StreamConflict) { CurrentVersion = currentVersion };
        }

        /// <summary>Builds a validation-failure result.</summary>
        /// <param name="errors">Every error at once.</param>
        /// <returns>The result.</returns>
        public static IntakeResultModel Invalid(IReadOnlyList<ValidationErrorModel> errors)
        {
            return new(IntakeOutcome.Invalid) { Errors = errors };
        }

        /// <summary>Builds a spec-unavailable result.</summary>
        /// <returns>The result.</returns>
        public static IntakeResultModel SpecUnavailable()
        {
            return new(IntakeOutcome.SpecUnavailable);
        }
    }

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
    }
}
