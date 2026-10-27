namespace Myss.Data
{
    using System;
    using System.Collections.Generic;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Registration details linked to the authenticated identity subject.
    /// </summary>
    public class MyssUserProfile
    {
        /// <summary>Gets or sets the profile identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the stable authenticated identity subject.</summary>
        public required string Subject { get; set; }

        /// <summary>Gets or sets the submitted first name.</summary>
        public required string FirstName { get; set; }

        /// <summary>Gets or sets the submitted last name.</summary>
        public required string LastName { get; set; }

        /// <summary>Gets or sets the submitted date of birth.</summary>
        public DateOnly DateOfBirth { get; set; }

        /// <summary>Gets or sets the submitted email address.</summary>
        public required string Email { get; set; }

        /// <summary>Gets or sets the submitted social insurance number.</summary>
        public required string Sin { get; set; }

        /// <summary>
        /// Gets or sets the submitted phone number, ten digits with no punctuation.
        /// Null for profiles registered before registration v4 asked for it.
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Gets or sets the submitted gender, as the option value of the registration
        /// form's gender field. Null for profiles registered before registration v4.
        /// </summary>
        public string? Gender { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the citizen wants a reminder when the
        /// online monthly report opens. Stored only: nothing sends it yet (MYSS-271).
        /// </summary>
        public bool MonthlyReportReminder { get; set; }

        /// <summary>
        /// Gets or sets the salted hash of the citizen's 4-digit PIN, stored in
        /// <c>taapcd_aae_passcode</c> (MYSS-258). Null until a PIN is set; only
        /// Basic BCeID users have one. Never the PIN itself.
        /// </summary>
        public string? PinHash { get; set; }

        /// <summary>
        /// Gets or sets how many wrong current PINs were given in a row while
        /// changing it. Reset by a correct one and when a lockout starts.
        /// </summary>
        public int PinFailedAttempts { get; set; }

        /// <summary>
        /// Gets or sets when the PIN lockout ends, or null when it is not locked.
        /// </summary>
        public DateTimeOffset? PinLockedUntil { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 hash (lower-case hex) of the single-use PIN
        /// reset token emailed to the citizen (MYSS-36, RULE-IDA-02). Null when no
        /// reset is outstanding, and nulled when the token is used. The token
        /// itself is a random GUID that exists only in the email.
        /// </summary>
        public string? PinResetTokenHash { get; set; }

        /// <summary>
        /// Gets or sets when the outstanding PIN reset token was issued; the link
        /// expires a configured time after it. Null with no reset outstanding.
        /// </summary>
        public DateTimeOffset? PinResetRequestedAt { get; set; }

        /// <summary>
        /// Gets or sets when <see cref="Email"/> was proven by its verification
        /// link (MYSS-305). Null while the address from registration is not yet
        /// verified. Profiles from before verification existed were marked
        /// verified by the migration that added it.
        /// </summary>
        public DateTimeOffset? EmailVerifiedAt { get; set; }

        /// <summary>
        /// Gets or sets a new address the citizen asked to change to, waiting for
        /// its verification link. <see cref="Email"/> stays the address in use
        /// until then. Only set while <see cref="Email"/> is verified: an
        /// unverified address is simply replaced.
        /// </summary>
        public string? PendingEmail { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 hash (lower-case hex) of the single-use email
        /// verification token, for <see cref="PendingEmail"/> when one is set and
        /// otherwise for an unverified <see cref="Email"/>. Null when nothing is
        /// waiting; a new link replaces it, and using the link nulls it.
        /// </summary>
        public string? EmailVerificationTokenHash { get; set; }

        /// <summary>
        /// Gets or sets when the outstanding verification link was sent; it
        /// expires a configured time after, and another cannot be sent until the
        /// cooldown after it has passed.
        /// </summary>
        public DateTimeOffset? EmailVerificationRequestedAt { get; set; }

        /// <summary>
        /// Gets or sets when the ICM contact match at registration found the
        /// citizen among ICM's contacts. Null when it found no match, and for
        /// profiles registered before the match existed. For the MVP the match
        /// is hardcoded to succeed.
        /// </summary>
        public DateTimeOffset? IcmContactMatchedAt { get; set; }

        /// <summary>Gets or sets the citizen's phone numbers, in their chosen order.</summary>
        public List<MyssUserPhone> Phones { get; set; } = [];

        /// <summary>Gets or sets when the profile was first created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Gets or sets when the profile was last updated.</summary>
        public DateTimeOffset UpdatedAt { get; set; }
    }
}