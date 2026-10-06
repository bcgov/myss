namespace Myss.Api.Domain
{
    using System.Linq;

    /// <summary>
    /// A ten-digit North American phone number: area code, exchange and line,
    /// shown to the citizen as <c>(250) 555-0123</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same branded-constructor pattern as <see cref="Sin"/>: the only way
    /// to obtain a value is <see cref="TryCreate"/>, so an unvalidated number
    /// cannot be stored. The Ministry format is <c>(area code) ###-####</c>
    /// (MYSS-271), so the stored form is the ten digits and the punctuation is
    /// presentation.
    /// </para>
    /// <para>
    /// <b>PII.</b> Like <see cref="Sin"/>, <see cref="object.ToString"/> is not
    /// overridden, so a number does not reach a log by accident.
    /// </para>
    /// </remarks>
    public sealed class PhoneNumber
    {
        private const int RequiredDigits = 10;

        private PhoneNumber(string digits) => Digits = digits;

        /// <summary>Gets the ten digits, formatting stripped.</summary>
        public string Digits { get; }

        /// <summary>
        /// Validates a candidate number, ignoring the punctuation people type
        /// around one.
        /// </summary>
        /// <param name="raw">The value as entered: <c>(250) 555-0123</c>, <c>250-555-0123</c>, <c>2505550123</c>.</param>
        /// <returns>A result carrying the validated number, or a failure keyword.</returns>
        public static DomainValidationResult<PhoneNumber> TryCreate(string? raw)
        {
            string value = raw ?? string.Empty;

            // Unlike the SIN, only the usual punctuation is skipped. A letter
            // or an extension is a different number, not a formatted one, so
            // it is refused rather than silently dropped.
            bool onlyDigitsAndPunctuation = value.All(c => char.IsAsciiDigit(c) || IsFormatting(c));
            string digits = new([.. value.Where(char.IsAsciiDigit)]);

            if (!onlyDigitsAndPunctuation || digits.Length != RequiredDigits)
            {
                return DomainValidationResult<PhoneNumber>.Fail(
                    ValidationKeywords.PhoneInvalidFormat,
                    "Enter a 10-digit phone number with the area code, like (250) 555-0123.");
            }

            return DomainValidationResult<PhoneNumber>.Ok(new PhoneNumber(digits));
        }

        private static bool IsFormatting(char c) => c is ' ' or '(' or ')' or '-' or '.';
    }
}
