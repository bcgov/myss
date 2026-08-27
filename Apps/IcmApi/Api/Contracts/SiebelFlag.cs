namespace Icm.Api.Contracts
{
    using System;

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
        /// <returns>
        /// The flag, or null when the field was absent or empty. Anything that is not
        /// <c>"Y"</c> is false — Siebel writes exactly one character here, so a value that
        /// is neither is a field that is not a flag at all, and reporting it as true would
        /// be the more dangerous guess.
        /// </returns>
        public static bool? ToBoolean(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : string.Equals(value.Trim(), Yes, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Writes a Siebel flag.
        /// </summary>
        /// <param name="value">The flag, or null to leave the field alone.</param>
        /// <returns><c>"Y"</c>, <c>"N"</c>, or null to omit the field from the request.</returns>
        public static string? FromBoolean(bool? value) =>
            value switch { true => Yes, false => No, null => null };
    }
}
