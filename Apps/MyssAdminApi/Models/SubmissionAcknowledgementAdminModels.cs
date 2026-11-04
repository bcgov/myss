namespace Myss.AdminApi.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Myss.Common.Content;
    using Myss.FormSpec;

    /// <summary>A form's confirmation message as the editor shows it: the live entry and the history.</summary>
    public sealed class SubmissionAcknowledgementAdminModel
    {
        /// <summary>Gets the live entry, or null when none is saved and applicants see the built-in message.</summary>
        public SubmissionAcknowledgementEntry? Current { get; init; }

        /// <summary>Gets the newest saved entries, newest first; the first is <see cref="Current"/>.</summary>
        public required IReadOnlyList<SubmissionAcknowledgementEntry> History { get; init; }
    }

    /// <summary>An editor's save of a form's confirmation message.</summary>
    public sealed class SaveSubmissionAcknowledgementRequestModel
    {
        /// <summary>Gets the ID of the live entry the edit started from, or null when none was saved.</summary>
        public string? BasedOn { get; init; }

        /// <summary>Gets the page heading.</summary>
        public string? Heading { get; init; }

        /// <summary>Gets the alert title.</summary>
        public string? Title { get; init; }

        /// <summary>Gets the message.</summary>
        public string? Body { get; init; }
    }

    /// <summary>
    /// The outcome of a confirmation message save: the saved message, the reasons it
    /// was refused, or the news that the live message changed after the edit began.
    /// </summary>
    public sealed class SubmissionAcknowledgementSaveResultModel
    {
        private SubmissionAcknowledgementSaveResultModel()
        {
        }

        /// <summary>Gets the form's message after the save. Null when the save was refused.</summary>
        public SubmissionAcknowledgementAdminModel? Saved { get; private init; }

        /// <summary>Gets every reason the save was refused.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; private init; } = [];

        /// <summary>Gets a value indicating whether the save was refused because the live message changed.</summary>
        public bool IsChanged { get; private init; }

        /// <summary>Gets a value indicating whether the save was accepted.</summary>
        [MemberNotNullWhen(true, nameof(Saved))]
        public bool IsValid => Errors.Count == 0;

        /// <summary>Creates an accepted result.</summary>
        /// <param name="saved">The form's message after the save.</param>
        /// <returns>An accepted result.</returns>
        public static SubmissionAcknowledgementSaveResultModel Accepted(SubmissionAcknowledgementAdminModel saved)
        {
            ArgumentNullException.ThrowIfNull(saved);
            return new() { Saved = saved };
        }

        /// <summary>Creates a result refused by the message rules.</summary>
        /// <param name="errors">Every reason for refusal; at least one.</param>
        /// <returns>A refused result.</returns>
        public static SubmissionAcknowledgementSaveResultModel Refused(IReadOnlyList<ValidationErrorModel> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentOutOfRangeException.ThrowIfZero(errors.Count, nameof(errors));
            return new() { Errors = errors };
        }

        /// <summary>Creates a result refused because the live message changed after the edit began.</summary>
        /// <param name="error">The reason, carrying the changed keyword.</param>
        /// <returns>A refused result.</returns>
        public static SubmissionAcknowledgementSaveResultModel Changed(ValidationErrorModel error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new() { Errors = [error], IsChanged = true };
        }
    }
}
