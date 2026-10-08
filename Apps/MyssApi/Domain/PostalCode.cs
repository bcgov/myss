namespace Myss.Api.Domain
{
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A Canadian postal code in the A1A 1A1 format, with or without the space.
    /// </summary>
    /// <remarks>
    /// The C# half of the branded-constructor pattern (see <see cref="Sin"/>).
    /// The TypeScript mirror is the <c>postalCode</c> rule in the webclient's
    /// <c>validationRules.ts</c>; both are driven by the <c>postalCode</c>
    /// vectors in <c>Shared/validation/validation-vectors.json</c>.
    /// </remarks>
    public sealed partial class PostalCode
    {
        private PostalCode(string value) => Value = value;

        /// <summary>Gets the postal code as "A1A 1A1": upper case, one space.</summary>
        public string Value { get; }

        /// <summary>Validates a candidate postal code.</summary>
        /// <param name="raw">The value as submitted.</param>
        /// <returns>A result carrying the validated code, or a failure keyword.</returns>
        public static DomainValidationResult<PostalCode> TryCreate(string? raw)
        {
            string trimmed = (raw ?? string.Empty).Trim();
            Match match = Pattern().Match(trimmed);

            if (!match.Success)
            {
                return DomainValidationResult<PostalCode>.Fail(
                    ValidationKeywords.PostalCodeInvalidFormat,
                    "Enter a postal code in the format A1A 1A1.");
            }

            string normalized = string.Create(
                CultureInfo.InvariantCulture,
                $"{match.Groups["fsa"].Value.ToUpperInvariant()} {match.Groups["ldu"].Value.ToUpperInvariant()}");
            return DomainValidationResult<PostalCode>.Ok(new PostalCode(normalized));
        }

        // [0-9], not \d: in .NET \d also matches other scripts' digits, which
        // the browser's rule never accepts.
        [GeneratedRegex(@"^(?<fsa>[A-Za-z][0-9][A-Za-z]) ?(?<ldu>[0-9][A-Za-z][0-9])$", RegexOptions.CultureInvariant)]
        private static partial Regex Pattern();
    }
}
