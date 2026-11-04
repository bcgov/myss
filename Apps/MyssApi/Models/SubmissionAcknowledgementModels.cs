namespace Myss.Api.Models
{
    /// <summary>
    /// The confirmation message a form's page shows after a successful submission.
    /// The texts only: who saved it is never part of this anonymous answer.
    /// </summary>
    public class SubmissionAcknowledgementModel
    {
        /// <summary>Gets the page heading.</summary>
        public required string Heading { get; init; }

        /// <summary>Gets the alert title.</summary>
        public required string Title { get; init; }

        /// <summary>Gets the message, in the formatting the web client renders.</summary>
        public required string Body { get; init; }
    }

    /// <summary>What a read of a form's live confirmation message found.</summary>
    public enum SubmissionAcknowledgementStatus
    {
        /// <summary>A message is published.</summary>
        Found,

        /// <summary>No message is published.</summary>
        None,

        /// <summary>The content engine could not be read.</summary>
        Unavailable,
    }

    /// <summary>A form's live confirmation message, or why there is none.</summary>
    public sealed class SubmissionAcknowledgementLookup
    {
        private SubmissionAcknowledgementLookup(
            SubmissionAcknowledgementStatus status,
            SubmissionAcknowledgementModel? acknowledgement)
        {
            this.Status = status;
            this.Acknowledgement = acknowledgement;
        }

        /// <summary>Gets the result when no message is published.</summary>
        public static SubmissionAcknowledgementLookup None { get; } =
            new(SubmissionAcknowledgementStatus.None, null);

        /// <summary>Gets the result when the content engine could not be read.</summary>
        public static SubmissionAcknowledgementLookup Unavailable { get; } =
            new(SubmissionAcknowledgementStatus.Unavailable, null);

        /// <summary>Gets what the read found.</summary>
        public SubmissionAcknowledgementStatus Status { get; }

        /// <summary>Gets the message when one is published, otherwise null.</summary>
        public SubmissionAcknowledgementModel? Acknowledgement { get; }

        /// <summary>The result when a message is published.</summary>
        /// <param name="acknowledgement">The live message.</param>
        /// <returns>The result.</returns>
        public static SubmissionAcknowledgementLookup Found(SubmissionAcknowledgementModel acknowledgement) =>
            new(SubmissionAcknowledgementStatus.Found, acknowledgement);
    }
}
