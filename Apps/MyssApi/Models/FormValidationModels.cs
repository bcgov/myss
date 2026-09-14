namespace Myss.Api.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// One rejected value: which field, why, and what to tell the citizen.
    /// </summary>
    /// <remarks>
    /// The shape is fixed by §7.2 of the assessment: <c>{ field, keyword,
    /// message }</c>. <c>field</c> lets the client build the WCAG-required
    /// error summary whose entries link to and focus the errored field;
    /// <c>keyword</c> is stable across wording changes and translation.
    /// </remarks>
    public class ValidationErrorModel
    {
        /// <summary>Gets or sets the component key the failure belongs to.</summary>
        public required string Field { get; set; }

        /// <summary>Gets or sets the stable failure keyword.</summary>
        public required string Keyword { get; set; }

        /// <summary>Gets or sets the human-readable message.</summary>
        public required string Message { get; set; }
    }

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

    /// <summary>
    /// The outcome of an admin form-spec write (save draft or publish): the
    /// value produced, or the reasons it was refused. Mirrors
    /// <see cref="FormSubmissionResultModel"/> so the controller can shape a
    /// 422-with-errors the same way for both.
    /// </summary>
    /// <typeparam name="T">The success payload type.</typeparam>
    public class FormSpecWriteResultModel<T>
        where T : class
    {
        /// <summary>Gets or sets the produced value. Null when validation failed.</summary>
        public T? Value { get; set; }

        /// <summary>Gets or sets every reason the write was refused.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; set; } = [];

        /// <summary>Gets a value indicating whether the write was accepted.</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>Creates an accepted result.</summary>
        /// <param name="value">The produced value.</param>
        /// <returns>An accepted result.</returns>
        public static FormSpecWriteResultModel<T> Accepted(T value) => new() { Value = value };

        /// <summary>Creates a refused result.</summary>
        /// <param name="errors">Every reason for refusal.</param>
        /// <returns>A refused result.</returns>
        public static FormSpecWriteResultModel<T> Refused(IReadOnlyList<ValidationErrorModel> errors) => new() { Errors = errors };
    }
}
