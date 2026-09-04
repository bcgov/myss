namespace Icm.Api.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Siebel's <c>"Y"</c> / <c>"N"</c> in place of a boolean, and the conversion to and
    /// from one.
    /// </summary>
    /// <remarks>
    /// Also the value of the <c>uniformresponse</c> and <c>pagination</c> query parameters,
    /// which use the same pair.
    /// </remarks>
    internal static class SiebelFlag
    {
        /// <summary>True.</summary>
        public const string Yes = "Y";

        /// <summary>False.</summary>
        public const string No = "N";

        /// <summary>
        /// Reads a Siebel flag.
        /// </summary>
        /// <param name="value">The field value.</param>
        /// <param name="field">The ICM field name, for the unparsed record.</param>
        /// <param name="unparsed">Collects values that could not be read.</param>
        /// <returns>
        /// The flag, or null when the field was absent, empty, or neither <c>"Y"</c> nor
        /// <c>"N"</c>. An unexpected value must not become an asserted answer in either
        /// direction — on <c>Restricted Flag</c>, "unknown" reported as "unrestricted"
        /// would be exactly as dangerous as reporting it restricted is useless — so it is
        /// recorded in <paramref name="unparsed"/> and returned as unknown instead.
        /// </returns>
        public static bool? ToBoolean(
            string? value, string field, IDictionary<string, string> unparsed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            if (string.Equals(trimmed, Yes, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(trimmed, No, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            unparsed[field] = value;
            return null;
        }

        /// <summary>
        /// Writes a Siebel flag.
        /// </summary>
        /// <param name="value">The flag, or null to leave the field alone.</param>
        /// <returns><c>"Y"</c>, <c>"N"</c>, or null to omit the field from the request.</returns>
        public static string? FromBoolean(bool? value) =>
            value switch { true => Yes, false => No, null => null };
    }
}
