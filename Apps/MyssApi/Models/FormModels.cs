namespace Myss.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Myss.FormSpec;

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

        /// <summary>
        /// Gets or sets the 4-digit PIN a Basic BCeID citizen creates at
        /// registration (MYSS-258). Registration only. Deliberately outside
        /// <see cref="Answers"/>, which are stored as submitted: only the PIN's
        /// salted hash is kept, on the profile.
        /// </summary>
        public string? Pin { get; set; }

        /// <summary>
        /// Gets or sets the PIN typed a second time, which must match <see cref="Pin"/>.
        /// </summary>
        public string? PinConfirmation { get; set; }
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
