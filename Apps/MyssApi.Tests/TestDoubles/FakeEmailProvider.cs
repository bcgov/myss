namespace Myss.Api.Tests.TestDoubles
{
    using System.Text.RegularExpressions;
    using Myss.Api.Providers;

    /// <summary>
    /// Records every email instead of sending it, or refuses when told to.
    /// </summary>
    public sealed partial class FakeEmailProvider : IEmailProvider
    {
        /// <summary>Gets the messages accepted so far, oldest first.</summary>
        public List<EmailMessage> Sent { get; } = [];

        /// <summary>Gets or sets a value indicating whether every send fails as Notify being down would.</summary>
        public bool Unavailable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether every send fails as Notify
        /// refusing the recipient's address would (MYSS-326).
        /// </summary>
        public bool RejectRecipient { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether every send fails as a
        /// timeout would: the message may have reached the service (MYSS-326).
        /// </summary>
        public bool TimesOut { get; set; }

        /// <inheritdoc/>
        public Task<string> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (TimesOut)
            {
                throw new EmailNotSentException("Fake email service did not answer in time.") { MayHaveBeenSent = true };
            }

            if (RejectRecipient)
            {
                throw new EmailNotSentException("Fake email service refused the address.") { RecipientRejected = true };
            }

            if (Unavailable)
            {
                throw new EmailNotSentException("Fake email service is unavailable.");
            }

            Sent.Add(message);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// The reset token in the last email's link, as the citizen's browser
        /// would read it from the fragment.
        /// </summary>
        /// <returns>The token.</returns>
        public string LastResetToken()
        {
            Match match = ResetToken().Match(Sent[^1].MarkdownBody);
            Assert.True(match.Success, "The last email carries no reset link.");
            return match.Groups[1].Value;
        }

        /// <summary>
        /// The email verification token in the last email's link (MYSS-305).
        /// </summary>
        /// <returns>The token.</returns>
        public string LastVerificationToken()
        {
            Match match = VerificationToken().Match(Sent[^1].MarkdownBody);
            Assert.True(match.Success, "The last email carries no verification link.");
            return match.Groups[1].Value;
        }

        [GeneratedRegex(@"/pin-reset#token=([0-9a-f-]{36})\)")]
        private static partial Regex ResetToken();

        [GeneratedRegex(@"/verify-email#token=([0-9a-f-]{36})\)")]
        private static partial Regex VerificationToken();
    }
}
