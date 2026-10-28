namespace Myss.FormSpec.Domain
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// An email address in a plausible shape, and the confirmation-match rule
    /// that accompanies it on intake forms.
    /// </summary>
    /// <remarks>
    /// Deliberately permissive. The only authoritative test of an address is
    /// sending mail to it; a regex that rejects unusual-but-legal addresses
    /// turns a validator into a barrier for people with perfectly good
    /// addresses. This checks for one <c>@</c>, a non-empty local part, a
    /// dotted domain, and no whitespace — enough to catch typing mistakes,
    /// not enough to argue with RFC 5322.
    /// </remarks>
    public sealed partial class EmailAddress
    {
        /// <summary>
        /// The longest address accepted: RFC 5321's 254, the most mail can be
        /// delivered to. Every accepted address therefore fits the 320-character
        /// <c>email</c> and <c>pending_email</c> columns (MYSS-305), so a long one
        /// is a field error rather than a failed save. The TypeScript mirror is
        /// <c>EMAIL_MAX_LENGTH</c> in <c>@myss/formio</c>.
        /// </summary>
        public const int MaxLength = 254;

        private EmailAddress(string value) => Value = value;

        /// <summary>Gets the trimmed address as submitted.</summary>
        public string Value { get; }

        /// <summary>Validates a candidate address.</summary>
        /// <param name="raw">The value as submitted.</param>
        /// <returns>A result carrying the validated address, or a failure keyword.</returns>
        public static DomainValidationResult<EmailAddress> TryCreate(string? raw)
        {
            string trimmed = (raw ?? string.Empty).Trim();

            if (!IsPlausible(trimmed))
            {
                return DomainValidationResult<EmailAddress>.Fail(
                    ValidationKeywords.EmailInvalidFormat,
                    "Enter an email address in the format name@example.com.");
            }

            return DomainValidationResult<EmailAddress>.Ok(new EmailAddress(trimmed));
        }

        /// <summary>
        /// Tests whether a confirmation field matches the address it confirms.
        /// </summary>
        /// <remarks>
        /// Compared case-insensitively after trimming. Mail domains are
        /// case-insensitive, and a citizen retyping their address with a
        /// different capitalisation has not made a mistake worth blocking them
        /// over. Local parts are technically case-sensitive; in practice no
        /// mail provider treats them that way, and treating them so here would
        /// produce false rejections.
        /// </remarks>
        /// <param name="value">The address.</param>
        /// <param name="confirmation">The confirmation field's value.</param>
        /// <returns>True when the two agree.</returns>
        public static bool ConfirmationMatches(string? value, string? confirmation) =>
            string.Equals(
                (value ?? string.Empty).Trim(),
                (confirmation ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// One <c>@</c> with something before it, no whitespace, and a domain of
        /// two or more labels (<see cref="IsDomainLabel"/>). Checked step by step
        /// rather than with one pattern, which backtracks polynomially on input
        /// like <c>!@!.!.!.…</c> (CodeQL polynomial ReDoS); every step is linear.
        /// The TypeScript mirror is <c>isEmailAddress</c> in <c>@myss/formio</c>.
        /// </summary>
        private static bool IsPlausible(string trimmed)
        {
            if (trimmed.Length > MaxLength || Whitespace().IsMatch(trimmed))
            {
                return false;
            }

            int at = trimmed.IndexOf('@', StringComparison.Ordinal);
            if (at < 1 || at != trimmed.LastIndexOf('@'))
            {
                return false;
            }

            // The local part stays permissive (it is the mailbox's business),
            // but the domain must be a domain: two or more labels of letters,
            // digits and hyphens (MYSS-326: "gov.bc.ca&&&&" was accepted, and
            // only the email service refused it).
            string[] labels = trimmed[(at + 1)..].Split('.');
            return labels.Length >= 2 && labels.All(IsDomainLabel);
        }

        /// <summary>
        /// One label of a domain name: 1 to 63 letters, digits or hyphens, not
        /// starting or ending with a hyphen. Letters and digits in any script.
        /// Mirrors <c>isDomainLabel</c> in <c>@myss/formio</c>.
        /// </summary>
        private static bool IsDomainLabel(string label) =>
            label.Length is >= 1 and <= 63
            && label[0] != '-'
            && label[^1] != '-'
            && label.All(c => char.IsLetterOrDigit(c) || c == '-');

        [GeneratedRegex(@"\s", RegexOptions.CultureInvariant)]
        private static partial Regex Whitespace();
    }
}
