namespace Myss.Api.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Sends email to a citizen (MYSS-36). Today that is BC Gov Notify
    /// (<see cref="NotifyEmailProvider"/>). Sending is asynchronous at the far
    /// end: a returned id means the message was accepted, not delivered.
    /// </summary>
    public interface IEmailProvider
    {
        /// <summary>
        /// Hands one email to the sending service.
        /// </summary>
        /// <param name="message">The recipient and the content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The sending service's id for the message, for tracing its delivery;
        /// empty when the service accepted it but its answer gave none.
        /// </returns>
        /// <exception cref="EmailNotSentException">When the message was not accepted.</exception>
        Task<string> SendAsync(EmailMessage message, CancellationToken cancellationToken);
    }

    /// <summary>
    /// One email to one recipient.
    /// </summary>
    /// <param name="To">The recipient's address. Personal information: never log it.</param>
    /// <param name="Subject">The subject line.</param>
    /// <param name="MarkdownBody">The body, as Markdown; the sending service renders it to HTML.</param>
    public sealed record EmailMessage(string To, string Subject, string MarkdownBody)
    {
        /// <summary>Returns the subject only, so a logged message does not carry the address.</summary>
        /// <returns>A description safe to log.</returns>
        public override string ToString() => $"EmailMessage {{ Subject = {Subject} }}";
    }

    /// <summary>
    /// The sending service did not accept a message: it is not configured,
    /// refused the request, or could not be reached in time.
    /// </summary>
    public sealed class EmailNotSentException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="EmailNotSentException"/> class.</summary>
        public EmailNotSentException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="EmailNotSentException"/> class.</summary>
        /// <param name="message">Why, without any recipient detail.</param>
        public EmailNotSentException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="EmailNotSentException"/> class.</summary>
        /// <param name="message">Why, without any recipient detail.</param>
        /// <param name="innerException">The underlying failure.</param>
        public EmailNotSentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Gets a value indicating whether the sending service refused the
        /// recipient's address itself (MYSS-326), rather than being unreachable
        /// or misconfigured: asking again later will not help, a different
        /// address might.
        /// </summary>
        public bool RecipientRejected { get; init; }

        /// <summary>
        /// Gets a value indicating whether the message may have reached the
        /// sending service despite the failure: a timeout or a broken connection
        /// after the request went out. A caller must not assume nothing was
        /// sent, and so must not take back a link it may already have delivered.
        /// </summary>
        public bool MayHaveBeenSent { get; init; }
    }
}
