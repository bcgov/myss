namespace Myss.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// A versioned Form.io specification served from the content engine.
    /// </summary>
    public class FormSpecModel
    {
        /// <summary>
        /// Gets or sets the logical form identifier.
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the spec version.
        /// </summary>
        public required int Version { get; set; }

        /// <summary>
        /// Gets or sets the human-readable title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the Form.io specification JSON.
        /// </summary>
        public required JsonElement Spec { get; set; }
    }

    /// <summary>
    /// A form submission request from the SPA.
    /// </summary>
    public class FormSubmissionRequestModel
    {
        /// <summary>
        /// Gets or sets the spec version the form was rendered with.
        /// </summary>
        public required int FormSpecVersion { get; set; }

        /// <summary>
        /// Gets or sets the submitted answers, keyed by component key.
        /// </summary>
        public required JsonElement Answers { get; set; }
    }

    /// <summary>
    /// A submission summary for list views. Does not include the answers.
    /// </summary>
    public class FormSubmissionSummaryModel
    {
        /// <summary>
        /// Gets or sets the submission identifier.
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the logical form identifier.
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the spec version in force at submit time.
        /// </summary>
        public required int FormSpecVersion { get; set; }

        /// <summary>
        /// Gets or sets the submission timestamp.
        /// </summary>
        public required DateTimeOffset SubmittedAt { get; set; }
    }

    /// <summary>
    /// A stored submission, returned together with the spec version it was
    /// submitted under.
    /// </summary>
    public class FormSubmissionResponseModel
    {
        /// <summary>
        /// Gets or sets the submission identifier.
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the logical form identifier.
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the spec version in force at submit time.
        /// </summary>
        public required int FormSpecVersion { get; set; }

        /// <summary>
        /// Gets or sets the submitted answers.
        /// </summary>
        public required JsonElement Answers { get; set; }

        /// <summary>
        /// Gets or sets the submission timestamp.
        /// </summary>
        public required DateTimeOffset SubmittedAt { get; set; }

        /// <summary>
        /// Gets or sets the spec version the submission was made against.
        /// Null on create responses.
        /// </summary>
        public FormSpecModel? Spec { get; set; }
    }

    /// <summary>
    /// One logical form and its versions, for the admin editor's forms list.
    /// </summary>
    public class FormSummaryModel
    {
        /// <summary>
        /// Gets or sets the logical form identifier.
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the human-readable title, or null when unset.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the versions of this form, oldest first.
        /// </summary>
        public required IReadOnlyList<FormVersionSummaryModel> Versions { get; set; }
    }

    /// <summary>
    /// One version of a form and whether it is published or a draft only.
    /// </summary>
    public class FormVersionSummaryModel
    {
        /// <summary>
        /// Gets or sets the version number.
        /// </summary>
        public required int Version { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this version has been
        /// published. False means the version exists only as an in-progress draft.
        /// </summary>
        public required bool IsPublished { get; set; }
    }

    /// <summary>
    /// The body of a save-draft request: the edited spec and its title.
    /// </summary>
    public class SaveDraftRequestModel
    {
        /// <summary>
        /// Gets or sets the edited Form.io specification JSON.
        /// </summary>
        public required JsonElement Spec { get; set; }

        /// <summary>
        /// Gets or sets the human-readable title, or null to leave it unset.
        /// </summary>
        public string? Title { get; set; }
    }

    /// <summary>
    /// The result of publishing a form: the version number that went live.
    /// </summary>
    public class PublishResultModel
    {
        /// <summary>
        /// Gets or sets the version number that was published.
        /// </summary>
        public required int Version { get; set; }
    }
}
