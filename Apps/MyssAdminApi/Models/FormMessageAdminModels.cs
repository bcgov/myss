namespace Myss.AdminApi.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Myss.Common.Content;
    using Myss.FormSpec;

    /// <summary>A form message as the editor shows it: what it is, the live entry and the history.</summary>
    public sealed class FormMessageAdminModel
    {
        /// <summary>Gets the message's definition: its name, where applicants see it, and its placeholders.</summary>
        public required FormMessageDefinition Definition { get; init; }

        /// <summary>Gets the live entry, or null when none is saved and applicants see the built-in message.</summary>
        public FormMessageEntry? Current { get; init; }

        /// <summary>Gets the newest saved entries, newest first; the first is <see cref="Current"/>.</summary>
        public required IReadOnlyList<FormMessageEntry> History { get; init; }
    }

    /// <summary>One defined form message as the list of messages shows it.</summary>
    public sealed class FormMessageSummaryModel
    {
        /// <summary>Gets the message's definition.</summary>
        public required FormMessageDefinition Definition { get; init; }

        /// <summary>Gets when the live entry was saved, or null when none is saved.</summary>
        public DateTimeOffset? LastChangedAt { get; init; }

        /// <summary>Gets the IDIR username of whoever saved the live entry, or null when none is saved.</summary>
        public string? LastChangedBy { get; init; }
    }

    /// <summary>An editor's save of a form message.</summary>
    public sealed class SaveFormMessageRequestModel
    {
        /// <summary>Gets the ID of the live entry the edit started from, or null when none was saved.</summary>
        public string? BasedOn { get; init; }

        /// <summary>Gets the page heading.</summary>
        public string? Heading { get; init; }

        /// <summary>Gets the title.</summary>
        public string? Title { get; init; }

        /// <summary>Gets the message.</summary>
        public string? Body { get; init; }
    }

    /// <summary>
    /// The outcome of a form message save: the saved message, the reasons it
    /// was refused, or the news that the live message changed after the edit began.
    /// </summary>
    public sealed class FormMessageSaveResultModel
    {
        private FormMessageSaveResultModel()
        {
        }

        /// <summary>Gets the message after the save. Null when the save was refused.</summary>
        public FormMessageAdminModel? Saved { get; private init; }

        /// <summary>Gets every reason the save was refused.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; private init; } = [];

        /// <summary>Gets a value indicating whether the save was refused because the live message changed.</summary>
        public bool IsChanged { get; private init; }

        /// <summary>Gets a value indicating whether the save was accepted.</summary>
        [MemberNotNullWhen(true, nameof(Saved))]
        public bool IsValid => Errors.Count == 0;

        /// <summary>Creates an accepted result.</summary>
        /// <param name="saved">The message after the save.</param>
        /// <returns>An accepted result.</returns>
        public static FormMessageSaveResultModel Accepted(FormMessageAdminModel saved)
        {
            ArgumentNullException.ThrowIfNull(saved);
            return new() { Saved = saved };
        }

        /// <summary>Creates a result refused by the message rules.</summary>
        /// <param name="errors">Every reason for refusal; at least one.</param>
        /// <returns>A refused result.</returns>
        public static FormMessageSaveResultModel Refused(IReadOnlyList<ValidationErrorModel> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentOutOfRangeException.ThrowIfZero(errors.Count, nameof(errors));
            return new() { Errors = errors };
        }

        /// <summary>Creates a result refused because the live message changed after the edit began.</summary>
        /// <param name="error">The reason, carrying the changed keyword.</param>
        /// <returns>A refused result.</returns>
        public static FormMessageSaveResultModel Changed(ValidationErrorModel error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new() { Errors = [error], IsChanged = true };
        }
    }
}
