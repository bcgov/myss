namespace Myss.Api.Models
{
    /// <summary>
    /// The texts of a form message as a form's page shows them. Who saved them is
    /// never part of this anonymous answer.
    /// </summary>
    public class FormMessageModel
    {
        /// <summary>Gets the page heading.</summary>
        public required string Heading { get; init; }

        /// <summary>Gets the title.</summary>
        public required string Title { get; init; }

        /// <summary>Gets the message, in the formatting the web client renders.</summary>
        public required string Body { get; init; }
    }

    /// <summary>What a read of a form message's live texts found.</summary>
    public enum FormMessageStatus
    {
        /// <summary>A message is published.</summary>
        Found,

        /// <summary>No message is published.</summary>
        None,

        /// <summary>The content engine could not be read.</summary>
        Unavailable,
    }

    /// <summary>A form message's live texts, or why there are none.</summary>
    public sealed class FormMessageLookup
    {
        private FormMessageLookup(FormMessageStatus status, FormMessageModel? message)
        {
            this.Status = status;
            this.Message = message;
        }

        /// <summary>Gets the result when no message is published.</summary>
        public static FormMessageLookup None { get; } =
            new(FormMessageStatus.None, null);

        /// <summary>Gets the result when the content engine could not be read.</summary>
        public static FormMessageLookup Unavailable { get; } =
            new(FormMessageStatus.Unavailable, null);

        /// <summary>Gets what the read found.</summary>
        public FormMessageStatus Status { get; }

        /// <summary>Gets the message when one is published, otherwise null.</summary>
        public FormMessageModel? Message { get; }

        /// <summary>The result when a message is published.</summary>
        /// <param name="message">The live message.</param>
        /// <returns>The result.</returns>
        public static FormMessageLookup Found(FormMessageModel message) =>
            new(FormMessageStatus.Found, message);
    }
}
