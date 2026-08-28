namespace Myss.Api.Domain
{
    using System;
    using System.Linq;

    /// <summary>
    /// A Social Insurance Number that has passed the Luhn mod-10 check.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The C# half of the branded-constructor pattern in Handbook §2.4. The
    /// constructor is private, so the only way to obtain a <see cref="Sin"/> is
    /// through <see cref="TryCreate"/> — a value of this type cannot exist
    /// without having been validated. The TypeScript <c>makeSin</c> built in
    /// Phase 1 is the mirror of this, and both are driven by the same vectors
    /// in <c>Shared/validation/validation-vectors.json</c> so they cannot
    /// silently disagree.
    /// </para>
    /// <para>
    /// Business rule BR-D1-02; ported from
    /// <c>myss-api/app/domains/registration/validators.py</c>.
    /// </para>
    /// <para>
    /// <b>PII.</b> This type deliberately does not override
    /// <see cref="ToString"/> to return the number. A SIN must be salted-hashed
    /// at rest and must never reach a log; making the raw value awkward to
    /// stringify by accident is the cheapest protection available. Use
    /// <see cref="Digits"/> explicitly when the value is genuinely needed.
    /// </para>
    /// </remarks>
    public sealed class Sin
    {
        private const int RequiredDigits = 9;

        private Sin(string digits) => Digits = digits;

        /// <summary>Gets the nine digits, formatting stripped.</summary>
        public string Digits { get; }

        /// <summary>
        /// Validates a candidate SIN, stripping any formatting first.
        /// </summary>
        /// <param name="raw">The value as submitted, possibly spaced or hyphenated.</param>
        /// <returns>A result carrying the validated SIN, or a failure keyword.</returns>
        public static DomainValidationResult<Sin> TryCreate(string? raw)
        {
            // Masking is presentation. A citizen pastes "046 454 286" from a
            // document and that must be accepted; the mask characters must never
            // reach the checksum.
            string digits = new([.. (raw ?? string.Empty).Where(char.IsAsciiDigit)]);

            if (digits.Length != RequiredDigits)
            {
                return DomainValidationResult<Sin>.Fail(
                    ValidationKeywords.SinWrongLength,
                    "A Social Insurance Number must be 9 digits.");
            }

            // All zeros satisfies the arithmetic but is not a SIN. Checked
            // explicitly, before Luhn, because Luhn would pass it.
            if (digits == new string('0', RequiredDigits) || !PassesLuhn(digits))
            {
                return DomainValidationResult<Sin>.Fail(
                    ValidationKeywords.SinInvalidChecksum,
                    "That Social Insurance Number is not valid. Check the digits and try again.");
            }

            return DomainValidationResult<Sin>.Ok(new Sin(digits));
        }

        /// <summary>Luhn mod-10 over nine digits, doubling every second digit from the left.</summary>
        /// <param name="digits">Exactly nine ASCII digits.</param>
        /// <returns>True when the checksum is satisfied.</returns>
        private static bool PassesLuhn(string digits)
        {
            int total = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                int n = digits[i] - '0';
                if (i % 2 == 1)
                {
                    n *= 2;
                    if (n > 9)
                    {
                        n -= 9;
                    }
                }

                total += n;
            }

            return total % 10 == 0;
        }

        /// <summary>
        /// Returns a redacted placeholder, never the number.
        /// </summary>
        /// <returns>A constant marker safe to appear in a log line.</returns>
        public override string ToString() => "[SIN redacted]";
    }
}
