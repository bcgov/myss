namespace Myss.Api.Domain
{
    using System.Linq;

    /// <summary>
    /// A North American phone number: ten digits once formatting is stripped,
    /// with an optional leading country code of 1.
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
        private const int RequiredDigits = 10;

        private PhoneNumber(string digits) => Digits = digits;

        /// <summary>Gets the ten digits, formatting and country code stripped.</summary>
        public string Digits { get; }

        /// <summary>
        /// Validates a candidate phone number, stripping any formatting first.
        /// </summary>
        /// <param name="raw">The value as submitted, possibly masked as "(250) 555-0199".</param>
        /// <returns>A result carrying the validated number, or a failure keyword.</returns>
        public static DomainValidationResult<PhoneNumber> TryCreate(string? raw)
        {
            // Masking is presentation: the mask characters never reach the check.
            string digits = new([.. (raw ?? string.Empty).Where(char.IsAsciiDigit)]);

            if (digits.Length == RequiredDigits + 1 && digits[0] == '1')
            {
                digits = digits[1..];
            }

            if (digits.Length != RequiredDigits)
            {
                return DomainValidationResult<PhoneNumber>.Fail(
                    ValidationKeywords.PhoneInvalidFormat,
                    "Enter a 10-digit phone number, for example 250 555 0199.");
            }

            return DomainValidationResult<PhoneNumber>.Ok(new PhoneNumber(digits));
        }
    }
