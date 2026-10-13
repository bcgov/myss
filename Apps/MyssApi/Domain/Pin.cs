namespace Myss.Api.Domain
{
    using System.Linq;

    /// <summary>
    /// A citizen's 4-digit Personal Identification Number: the electronic
    /// signature credential a Basic BCeID user sets at registration and
    /// changes on Account Info (MYSS-258, RULE-IDA-01).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same branded-constructor pattern as <see cref="Sin"/>: the only way
    /// to obtain a value is <see cref="TryCreate"/>. Unlike a SIN or a phone
    /// number nothing is stripped, because a PIN is typed into a field of its
    /// own rather than pasted from a document: "12 34" is not a PIN.
    /// </para>
    /// <para>
    /// The browser mirror is <c>checkPin</c> in the webclient's
    /// <c>lib/pin.ts</c>; both are driven by the <c>pin</c> vectors in
    /// <c>Shared/validation/validation-vectors.json</c>. It is deliberately not
    /// a Form.io rule: registration answers are stored as submitted, so a PIN
    /// must never be a form field.
    /// </para>
    /// <para>
    /// <b>Secret.</b> Only its salted hash is stored
    /// (<see cref="Services.IPinHasher"/>). <see cref="ToString"/> is redacted,
    /// so a PIN does not reach a log by accident.
    /// </para>
    /// </remarks>
    public sealed class Pin
    {
        private const int RequiredDigits = 4;

        private Pin(string digits) => Digits = digits;

        /// <summary>Gets the four digits.</summary>
        public string Digits { get; }

        /// <summary>
        /// Validates a candidate PIN: exactly four ASCII digits, nothing else.
        /// </summary>
        /// <param name="raw">The value as typed.</param>
        /// <returns>A result carrying the validated PIN, or a failure keyword.</returns>
        public static DomainValidationResult<Pin> TryCreate(string? raw)
        {
            string value = raw ?? string.Empty;
            if (value.Length != RequiredDigits || !value.All(char.IsAsciiDigit))
            {
                return DomainValidationResult<Pin>.Fail(
                    ValidationKeywords.PinInvalidFormat,
                    "Enter a 4-digit PIN using numbers only.");
            }

            return DomainValidationResult<Pin>.Ok(new Pin(value));
        }

        /// <summary>
        /// Returns a redacted placeholder, never the digits.
        /// </summary>
        /// <returns>A constant marker safe to appear in a log line.</returns>
        public override string ToString() => "[PIN redacted]";
    }
}
