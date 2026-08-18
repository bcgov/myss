namespace Myss.Api.Domain
{
    using System;
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
        private EmailAddress(string value) => Value = value;

        /// <summary>Gets the trimmed address as submitted.</summary>
        public string Value { get; }

        /// <summary>Validates a candidate address.</summary>
        /// <param name="raw">The value as submitted.</param>
        /// <returns>A result carrying the validated address, or a failure keyword.</returns>
        public static DomainValidationResult<EmailAddress> TryCreate(string? raw)
        {
            string trimmed = (raw ?? string.Empty).Trim();

            if (trimmed.Length == 0 || !Pattern().IsMatch(trimmed))
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

        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
        private static partial Regex Pattern();
    }
}
