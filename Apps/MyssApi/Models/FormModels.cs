namespace Myss.Api.Models
{
    using System;
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
}
