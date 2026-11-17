namespace Myss.AdminApi.Intake
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Text.Json;
    using Myss.Data.Intake;
    using Myss.FormSpec;

    /// <summary>
    /// A worker override Application for Assistance (MYSS-321) as the worker
    /// who fills it in sees it: a draft any worker may open, or what was
    /// submitted.
    /// </summary>
    public class WorkerOverrideApplicationModel
    {
        /// <summary>Gets or sets the application identifier.</summary>
        public required Guid Id { get; set; }

        /// <summary>Gets or sets the request number, the one the review queue shows.</summary>
        public required string ReferenceNumber { get; set; }

        /// <summary>Gets or sets the status code (<see cref="ApplicationStatusCodes"/>).</summary>
        public required string Status { get; set; }

        /// <summary>
        /// Gets or sets the row version, which a save or submit must send back:
        /// two workers on one draft cannot overwrite each other.
        /// </summary>
        public required int Version { get; set; }

        /// <summary>Gets or sets the logical form identifier.</summary>
        public required string FormSpecId { get; set; }

        /// <summary>Gets or sets the spec version the application was started on.</summary>
        public required int FormSpecVersion { get; set; }

        /// <summary>Gets or sets when the draft was started.</summary>
        public required DateTimeOffset CreatedAt { get; set; }

        /// <summary>Gets or sets when the answers were last saved.</summary>
        public required DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Gets or sets when it was first submitted; null for a draft.</summary>
        public DateTimeOffset? SubmittedAt { get; set; }

        /// <summary>Gets or sets the answers: the draft's, or what was submitted.</summary>
        public required JsonElement Answers { get; set; }

        /// <summary>Gets or sets the spec that renders it, when the call returns one.</summary>
        public FormSpecModel? Spec { get; set; }

        /// <summary>
        /// Gets or sets the worker override account the submit made, or null
        /// before the first submit.
        /// </summary>
        public Guid? AccountId { get; set; }
    }

    /// <summary>
    /// A worker override application not yet submitted, as the draft list
    /// (MYSS-322) shows it: enough to recognise it and open it.
    /// </summary>
    public class WorkerOverrideDraftSummaryModel
    {
        /// <summary>Gets or sets the application identifier.</summary>
        public required Guid Id { get; set; }

        /// <summary>Gets or sets the request number.</summary>
        public required string ReferenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the client's name as the draft has it so far (first
        /// and last name), or null while neither has been entered.
        /// </summary>
        public string? ClientName { get; set; }

        /// <summary>
        /// Gets or sets the IDIR username of the worker who started it; null
        /// for a draft started before this was recorded.
        /// </summary>
        public string? StartedBy { get; set; }

        /// <summary>Gets or sets when the draft was started.</summary>
        public required DateTimeOffset CreatedAt { get; set; }

        /// <summary>Gets or sets when its answers were last saved.</summary>
        public required DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>The answers a worker saves or submits, with the row version they last read.</summary>
    public class WorkerOverrideAnswersRequestModel
    {
        /// <summary>Gets or sets the row version last read.</summary>
        [Required]
        public int Version { get; set; }

        /// <summary>Gets or sets the answers, keyed as the form's components.</summary>
        public JsonElement Answers { get; set; }
    }

    /// <summary>How a worker override call went.</summary>
    public enum WorkerOverrideOutcome
    {
        /// <summary>Done; the application is attached.</summary>
        Ok,

        /// <summary>No worker override application has that id.</summary>
        NotFound,

        /// <summary>No version of the worker override form is published yet.</summary>
        SpecNotPublished,

        /// <summary>The form version the application was started on could not be read.</summary>
        SpecUnavailable,

        /// <summary>It is not a draft or returned, so it cannot be changed.</summary>
        NotEditable,

        /// <summary>Another save landed first; the caller reloads.</summary>
        StaleVersion,

        /// <summary>The answers failed validation; the errors are attached.</summary>
        Invalid,
    }

    /// <summary>The result of a worker override call.</summary>
    public sealed class WorkerOverrideResultModel
    {
        private WorkerOverrideResultModel(WorkerOverrideOutcome outcome)
        {
            Outcome = outcome;
        }

        /// <summary>Gets how the call went.</summary>
        public WorkerOverrideOutcome Outcome { get; }

        /// <summary>Gets the application, on <see cref="WorkerOverrideOutcome.Ok"/>.</summary>
        public WorkerOverrideApplicationModel? Application { get; private init; }

        /// <summary>Gets the field errors, on <see cref="WorkerOverrideOutcome.Invalid"/>.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; private init; } = [];

        /// <summary>Gets the current row version, when it is known, for a refusal.</summary>
        public int? CurrentVersion { get; private init; }

        /// <summary>Done.</summary>
        /// <param name="application">The application.</param>
        /// <returns>The result.</returns>
        public static WorkerOverrideResultModel Ok(WorkerOverrideApplicationModel application) =>
            new(WorkerOverrideOutcome.Ok) { Application = application };

        /// <summary>No such application.</summary>
        /// <returns>The result.</returns>
        public static WorkerOverrideResultModel NotFound() => new(WorkerOverrideOutcome.NotFound);

        /// <summary>The form is not published.</summary>
        /// <returns>The result.</returns>
        public static WorkerOverrideResultModel SpecNotPublished() => new(WorkerOverrideOutcome.SpecNotPublished);

        /// <summary>The form version could not be read.</summary>
        /// <returns>The result.</returns>
        public static WorkerOverrideResultModel SpecUnavailable() => new(WorkerOverrideOutcome.SpecUnavailable);

        /// <summary>It cannot be changed in its state.</summary>
        /// <param name="currentVersion">The row version.</param>
        /// <returns>The result.</returns>
        public static WorkerOverrideResultModel NotEditable(int currentVersion) =>
            new(WorkerOverrideOutcome.NotEditable) { CurrentVersion = currentVersion };

        /// <summary>Another save landed first.</summary>
        /// <param name="currentVersion">The row version, when known.</param>
        /// <returns>The result.</returns>
        public static WorkerOverrideResultModel StaleVersion(int? currentVersion) =>
            new(WorkerOverrideOutcome.StaleVersion) { CurrentVersion = currentVersion };

        /// <summary>The answers failed validation.</summary>
        /// <param name="errors">The field errors.</param>
        /// <returns>The result.</returns>
        public static WorkerOverrideResultModel Invalid(IReadOnlyList<ValidationErrorModel> errors) =>
            new(WorkerOverrideOutcome.Invalid) { Errors = errors };
    }
}
