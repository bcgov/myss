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

        /// <inheritdoc/>
        public Task<string> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
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

        [GeneratedRegex(@"/pin-reset#token=([0-9a-f-]{36})\)")]
        private static partial Regex ResetToken();
    }
}
