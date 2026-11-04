namespace Myss.Common.Content
{
    using System;

    /// <summary>
    /// One stored confirmation message for a form: what an applicant sees after a
    /// successful submission, and who saved it when.
    /// </summary>
    public sealed class SubmissionAcknowledgementEntry
    {
        /// <summary>Gets the content engine's document ID for the entry.</summary>
        public required string Id { get; init; }

        /// <summary>Gets the page heading.</summary>
        public required string Heading { get; init; }

        /// <summary>Gets the alert title.</summary>
        public required string Title { get; init; }

        /// <summary>Gets the message, in the formatting the web clients render.</summary>
        public required string Body { get; init; }

        /// <summary>Gets the IDIR username of whoever saved the entry.</summary>
        public required string ChangedBy { get; init; }

        /// <summary>Gets when the entry was published, which is when it was saved.</summary>
        public required DateTimeOffset ChangedAt { get; init; }
    }
}
