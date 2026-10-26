namespace Myss.Api.Configuration.Models
{
    using System;

    /// <summary>
    /// The emailed email-verification link (MYSS-305), bound from
    /// <c>Account:EmailVerification</c>: how long it works and how often one may
    /// be sent. The link's base URL is the webclient's, set once as
    /// <see cref="PinResetConfig.LinkBaseUrl"/>.
    /// </summary>
    public class EmailVerificationConfig
    {
        /// <summary>
        /// The webclient route the link opens. Must match <c>paths.verifyEmail</c>
        /// in the webclient's <c>routes/paths.ts</c>.
        /// </summary>
        public const string LinkPath = "verify-email";

        /// <summary>
        /// Gets or sets how long a verification link works after it is sent, in
        /// minutes (env var <c>Myss_Account__EmailVerification__TokenLifetimeMinutes</c>).
        /// Longer than a PIN reset's: a citizen may not open their mail at once,
        /// and the link only proves they can read the address.
        /// </summary>
        public int TokenLifetimeMinutes { get; set; } = 1440;

        /// <summary>
        /// Gets or sets how long a citizen waits after one verification email
        /// before another is sent, in seconds (env var
        /// <c>Myss_Account__EmailVerification__RequestCooldownSeconds</c>). Every
        /// change and resend sends an email, so this caps what one account can
        /// send to a mailbox, its own or anyone's.
        /// </summary>
        public int RequestCooldownSeconds { get; set; } = 60;

        /// <summary>Gets the link lifetime.</summary>
        public TimeSpan TokenLifetime => TimeSpan.FromMinutes(TokenLifetimeMinutes);

        /// <summary>Gets the wait between verification emails.</summary>
        public TimeSpan RequestCooldown => TimeSpan.FromSeconds(RequestCooldownSeconds);

        /// <summary>
        /// Refuses a lifetime that would make every link dead on arrival, and a
        /// cooldown that would not hold.
        /// </summary>
        /// <param name="config">The bound configuration.</param>
        /// <exception cref="InvalidOperationException">When either value is below 1.</exception>
        public static void Validate(EmailVerificationConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            if (config.TokenLifetimeMinutes < 1 || config.RequestCooldownSeconds < 1)
            {
                throw new InvalidOperationException(
                    "Account:EmailVerification is not usable: TokenLifetimeMinutes and RequestCooldownSeconds "
                    + $"must both be at least 1 (got {config.TokenLifetimeMinutes} and {config.RequestCooldownSeconds}).");
            }
        }
    }
}
