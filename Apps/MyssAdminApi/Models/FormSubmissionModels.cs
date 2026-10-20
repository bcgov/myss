namespace Myss.AdminApi.Models
{
    using System;
    using System.Text.Json;
    using Myss.FormSpec;

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
