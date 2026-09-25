namespace Icm.Api.Contracts
{
    using System;
    using System.Linq;

    /// <summary>
    /// The security control on values that go into a Siebel search expression: nothing
    /// reaches a <c>searchspec</c> from <see cref="ContactMapper"/> or
    /// <see cref="CaseMapper"/> without passing through here.
    /// </summary>
    /// <remarks>
    /// See <see cref="Models.ContactQuery"/> for what was measured: a value carrying a
    /// double quote closes its literal and the rest is read as more expression, and no
    /// escape syntax has been measured. So the library builds every expression itself and
    /// refuses any value that could change one. Blank values are refused too — an empty
    /// literal is a real search, for everyone with nothing on file, and the usual cause is
    /// a value that went missing upstream.
    /// </remarks>
    internal static class SiebelSearchValue
    {
        /// <summary>Checks a value is safe to interpolate into a search expression.</summary>
        /// <param name="value">The value.</param>
        /// <param name="property">The query property it came from, for the message.</param>
        /// <returns>The value, unchanged.</returns>
        /// <exception cref="ArgumentException">
        /// The value is blank, or contains a double quote, a Siebel wildcard (<c>*</c> or
        /// <c>?</c>) or a control character. The message names the property, never the
        /// value.
        /// </exception>
        public static string Vet(string value, string property)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"{property} is blank. Leave a criterion null to leave it out; a blank one "
                    + "would match every record with nothing on file for it.",
                    property);
            }

            // Never the value in the message: it is a SIN, a name, a card number.
            return value.Any(c => c is '"' or '*' or '?' || char.IsControl(c))
                ? throw new ArgumentException(
                    $"{property} contains a character that is not allowed in an ICM search "
                    + "(a double quote, * or ?, or a control character).",
                    property)
                : value;
        }
    }
}
