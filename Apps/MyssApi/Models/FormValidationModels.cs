namespace Myss.Api.Models
{
    using System.Collections.Generic;
    using Myss.FormSpec;

    /// <summary>
    /// The outcome of a submission attempt: the stored submission, or the
    /// reasons it was refused.
    /// </summary>
    /// <remarks>
    /// A returned result rather than a thrown exception. Validation failure is
    /// an ordinary, expected outcome of a public form — not an exceptional
    /// condition — and returning it keeps the service testable without
    /// exception plumbing.
    /// </remarks>
    public class FormSubmissionResultModel
    {
        /// <summary>Gets or sets the stored submission. Null when validation failed.</summary>
        public FormSubmissionResponseModel? Submission { get; set; }

        /// <summary>Gets or sets every reason the submission was refused.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; set; } = [];

        /// <summary>Gets a value indicating whether the submission was accepted and stored.</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>Creates an accepted result.</summary>
        /// <param name="submission">The stored submission.</param>
        /// <returns>A valid result.</returns>
        public static FormSubmissionResultModel Accepted(FormSubmissionResponseModel submission) =>
            new() { Submission = submission };

        /// <summary>Creates a refused result.</summary>
        /// <param name="errors">Every reason for refusal.</param>
        /// <returns>An invalid result.</returns>
        public static FormSubmissionResultModel Refused(
            IReadOnlyList<ValidationErrorModel> errors
        ) => new() { Errors = errors };
    }

}
