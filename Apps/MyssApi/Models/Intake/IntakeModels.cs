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
}
