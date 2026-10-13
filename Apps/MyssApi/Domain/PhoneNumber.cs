namespace Myss.Api.Domain
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A phone number accepted by the legacy MySS format rule.
    /// </summary>
    /// <remarks>
    /// The C# half of the branded-constructor pattern (see <see cref="Sin"/>).
    /// The TypeScript mirror is the <c>phone</c> rule in the webclient's
    /// <c>validationRules.ts</c>; both are driven by the <c>phone</c> vectors
    /// in <c>Shared/validation/validation-vectors.json</c>.
    /// <para>
    /// <b>PII.</b> Like <see cref="Sin"/>, <see cref="object.ToString"/> is not
    /// overridden, so a number does not reach a log by accident.
    /// </para>
    /// </remarks>
    public sealed class PhoneNumber
    {
        private static readonly Regex Pattern = new(
            @"^\(?([2-9][0-9][0-9])\)?[\s.-]?([0-9]{3})[\s.-]?([0-9]{4})$",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        private PhoneNumber(string digits) => Digits = digits;

        /// <summary>Gets the ten digits with formatting stripped.</summary>
        public string Digits { get; }

        /// <summary>
        /// Validates a candidate against the legacy phone format.
        /// </summary>
        /// <param name="raw">The value as submitted, possibly masked as "(250) 555-0199".</param>
        /// <returns>A result carrying the validated number, or a failure keyword.</returns>
        public static DomainValidationResult<PhoneNumber> TryCreate(string? raw)
        {
            Match match = Pattern.Match(raw ?? string.Empty);
            // .NET's $ can also match just before a trailing newline.
            if (raw is null || !match.Success || match.Length != raw.Length)
            {
                return DomainValidationResult<PhoneNumber>.Fail(
                    ValidationKeywords.PhoneInvalidFormat,
                    "Phone number is invalid");
            }

            return DomainValidationResult<PhoneNumber>.Ok(
                new PhoneNumber(new string(raw.Where(char.IsAsciiDigit).ToArray())));
        }
    }
}
