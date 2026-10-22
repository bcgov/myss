namespace Myss.AdminApi.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Myss.FormSpec;

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

    /// <summary>
    /// The outcome of an admin form-spec write (save draft or publish): the
    /// value produced, or the reasons it was refused. Mirrors
    /// MyssApi's <c>FormSubmissionResultModel</c> so the controller can shape a
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
    }

    /// <summary>Creates <see cref="FormSpecWriteResultModel{T}"/> values.</summary>
    public static class FormSpecWriteResultModel
    {
        /// <summary>Creates an accepted result.</summary>
        /// <typeparam name="T">The success payload type.</typeparam>
        /// <param name="value">The produced value.</param>
        /// <returns>An accepted result.</returns>
        public static FormSpecWriteResultModel<T> Accepted<T>(T value)
            where T : class => new() { Value = value };

        /// <summary>Creates a refused result.</summary>
        /// <typeparam name="T">The success payload type.</typeparam>
        /// <param name="errors">Every reason for refusal.</param>
        /// <returns>A refused result.</returns>
        public static FormSpecWriteResultModel<T> Refused<T>(IReadOnlyList<ValidationErrorModel> errors)
            where T : class => new() { Errors = errors };
    }
}
