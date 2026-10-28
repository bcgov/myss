namespace Myss.Data
{
    using System;

    /// <summary>
    /// An email address being proven during registration (MYSS-295), before the
    /// profile exists: registration's Step 2 sends a link to it, and the profile
    /// can only be created with it once the link has been followed. One row per
    /// signed-in identity; consumed when the registration completes.
    /// </summary>
    public class RegistrationEmailVerification
    {
        /// <summary>Gets or sets the authenticated identity subject registering.</summary>
        public required string Subject { get; set; }

        /// <summary>Gets or sets the address being proven, trimmed.</summary>
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 hash (lower-case hex) of the outstanding
        /// link's token, or null once it has been used.
        /// </summary>
        public string? TokenHash { get; set; }

        /// <summary>Gets or sets when the last link was sent: its expiry and the cooldown run from it.</summary>
        public DateTimeOffset RequestedAt { get; set; }

        /// <summary>Gets or sets when <see cref="Email"/> was proven, or null while it is not.</summary>
        public DateTimeOffset? VerifiedAt { get; set; }
    }
}
