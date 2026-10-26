namespace Myss.Api.Configuration.Models
{
    using System;

    /// <summary>
    /// The emailed PIN reset link (MYSS-36), bound from <c>Account:PinReset</c>:
    /// how long it works and where it points.
    /// </summary>
    public class PinResetConfig
    {
        /// <summary>
        /// The webclient route the link opens. Must match <c>paths.pinReset</c>
        /// in the webclient's <c>routes/paths.ts</c>.
        /// </summary>
        public const string LinkPath = "pin-reset";

        /// <summary>
        /// Gets or sets how long a reset link works after it is sent, in minutes
        /// (AC7; env var <c>Myss_Account__PinReset__TokenLifetimeMinutes</c>).
        /// </summary>
        public int TokenLifetimeMinutes { get; set; } = 60;

        /// <summary>
        /// Gets or sets how long a citizen waits after one reset email before
        /// asking for another, in seconds (env var
        /// <c>Myss_Account__PinReset__RequestCooldownSeconds</c>). Every request
        /// sends an email, so this caps what one account, or a stolen access
        /// token, can send to a mailbox and through Notify.
        /// </summary>
        public int RequestCooldownSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the webclient's public origin the link is built on, e.g.
        /// <c>https://myss.example.gov.bc.ca</c>. Checked at the first request, not
        /// at startup, as the Notify settings are.
        /// </summary>
        public Uri? LinkBaseUrl { get; set; }

        /// <summary>Gets the link lifetime.</summary>
        public TimeSpan TokenLifetime => TimeSpan.FromMinutes(TokenLifetimeMinutes);

        /// <summary>Gets the wait between reset emails.</summary>
        public TimeSpan RequestCooldown => TimeSpan.FromSeconds(RequestCooldownSeconds);

        /// <summary>
        /// Refuses a lifetime that would make every link dead on arrival, and a
        /// base URL that is not HTTPS. The token rides in the fragment, which is
        /// never sent, but a page served over plain HTTP can be altered in
        /// transit to read it; so HTTP is allowed only on a loopback host, for
        /// the local Vite dev server.
        /// </summary>
        /// <param name="config">The bound configuration.</param>
        /// <exception cref="InvalidOperationException">When either value is unusable.</exception>
        public static void Validate(PinResetConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            if (config.TokenLifetimeMinutes < 1)
            {
                throw new InvalidOperationException(
                    "Account:PinReset is not usable: TokenLifetimeMinutes must be at least 1 "
                    + $"(got {config.TokenLifetimeMinutes}).");
            }

            if (config.RequestCooldownSeconds < 1)
            {
                throw new InvalidOperationException(
                    "Account:PinReset is not usable: RequestCooldownSeconds must be at least 1 "
                    + $"(got {config.RequestCooldownSeconds}).");
            }

            if (config.LinkBaseUrl is { } url
                && !(url.IsAbsoluteUri
                    && (url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback))))
            {
                throw new InvalidOperationException(
                    "Account:PinReset is not usable: LinkBaseUrl must be an absolute https URL "
                    + "(http is accepted only for localhost, in local development).");
            }
        }

        /// <summary>
        /// Builds the reset link for a token.
        /// </summary>
        /// <param name="token">The reset token.</param>
        /// <returns>The link to email.</returns>
        /// <exception cref="InvalidOperationException">When <see cref="LinkBaseUrl"/> is not set.</exception>
        public Uri BuildLink(Guid token) => BuildLink(LinkPath, token);

        /// <summary>
        /// Builds a link to a webclient page for a token. <see cref="LinkBaseUrl"/>
        /// is the webclient's public URL for every emailed link, the email
        /// verification one (MYSS-305) included, so a deployment sets it once.
        /// The token goes in the fragment, which the browser never sends to a
        /// server, so it stays out of access logs and Referer headers.
        /// </summary>
        /// <param name="path">The webclient route, without a leading slash.</param>
        /// <param name="token">The token.</param>
        /// <returns>The link to email.</returns>
        /// <exception cref="InvalidOperationException">When <see cref="LinkBaseUrl"/> is not set.</exception>
        public Uri BuildLink(string path, Guid token)
        {
            if (LinkBaseUrl is null)
            {
                throw new InvalidOperationException(
                    "Account:PinReset:LinkBaseUrl is not set, so an emailed link cannot be built. "
                    + "Locally: appsettings.Development.json points at the Vite dev server. "
                    + "Deployed: set Myss_Account__PinReset__LinkBaseUrl to the webclient's public URL.");
            }

            return new Uri($"{LinkBaseUrl.AbsoluteUri.TrimEnd('/')}/{path}#token={token:D}");
        }
    }
}
