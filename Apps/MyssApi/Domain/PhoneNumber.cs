namespace Myss.Api.Domain
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>A phone number accepted by the legacy MySS phone rule.</summary>
    public sealed class PhoneNumber
    {
        private static readonly Regex Pattern = new(
            @"^\(?([2-9][0-9][0-9])\)?[\s.-]?([0-9]{3})[\s.-]?([0-9]{4})$",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        private PhoneNumber(string digits) => Digits = digits;

        /// <summary>Gets the ten digits without formatting.</summary>
        public string Digits { get; }

        /// <summary>Validates a phone number against the legacy MySS rule.</summary>
        /// <param name="raw">The number as entered.</param>
        /// <returns>The validated number or a failure with a keyword.</returns>
        public static DomainValidationResult<PhoneNumber> TryCreate(string? raw)
        {
            // .NET's $ can match before a final newline; the legacy annotation requires a full match.
            Match match = Pattern.Match(raw ?? string.Empty);
            if (raw is null || !match.Success || match.Length != raw.Length)
            {
                return DomainValidationResult<PhoneNumber>.Fail(
                    ValidationKeywords.PhoneInvalidFormat,
                    "Phone number is invalid");
            }

            return DomainValidationResult<PhoneNumber>.Ok(
                new PhoneNumber(new string(raw.Where(char.IsAsciiDigit).ToArray())));
        }

        /// <summary>Returns a placeholder safe for logs.</summary>
        /// <returns>A redacted value.</returns>
        public override string ToString() => "[phone number redacted]";
    }
}