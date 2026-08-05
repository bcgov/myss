namespace Myss.Api.Data
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// A stored form submission: the answers plus the spec version they were
    /// submitted under.
    /// </summary>
    public class FormSubmission
    {
        /// <summary>
        /// Gets or sets the submission identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the logical form identifier (e.g. "poc-test-form").
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the form-spec version in force at submit time.
        /// </summary>
        public int FormSpecVersion { get; set; }

        /// <summary>
        /// Gets or sets the submitted answers, keyed by component key.
        /// </summary>
        public required JsonDocument Answers { get; set; }

        /// <summary>
        /// Gets or sets the submission timestamp.
        /// </summary>
        public DateTimeOffset SubmittedAt { get; set; }
    }
}
