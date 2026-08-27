namespace Icm.Api.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Converts between Siebel's date fields and .NET date types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Siebel's date and time grammar is <b>ISO 8601</b>:
    /// <c>YYYY-MM-DDTHH:mm:ss.ffffff±HH:mm</c>, on a 24-hour clock, with the fractional
    /// seconds and the UTC offset optional and the offset recommended. See
    /// <see href="https://docs.oracle.com/en/applications/siebel/siebel-crm/26.3/szapc/c-Date-and-Time-Formats-ja1008698.html">
    /// Siebel: Date and Time Formats</see>. The spec this client is generated from types
    /// every date field but states no format, so that page is the authority.
    /// </para>
    /// <para>
    /// <b>Only ISO shapes are accepted.</b> Siebel's <c>MM/DD/YYYY</c> display format is
    /// deliberately not in the list: <c>03/04/2026</c> is 4 March read one way and 3 April
    /// read the other, and nothing in the value says which. Accepting it would mean
    /// guessing, and a wrong guess is silently wrong for every day of the month up to the
    /// twelfth. A value in that shape is reported through
    /// <see cref="Icm.Api.Models.ServiceRequest.UnparsedValues"/> with the raw text intact
    /// instead — if ICM turns out to send it, add the format here with the order confirmed,
    /// rather than letting it through on an assumption.
    /// </para>
    /// <para>
    /// The three Siebel date types map to three different .NET types on purpose.
    /// <c>DTYPE_UTCDATETIME</c> is an instant and becomes a <see cref="DateTimeOffset"/>;
    /// <c>DTYPE_DATETIME</c> carries no zone, so it becomes a <see cref="DateTime"/> with
    /// <see cref="DateTimeKind.Unspecified"/> rather than having an offset invented for it;
    /// <c>DTYPE_DATE</c> becomes a <see cref="DateOnly"/>. That last one matters more than
    /// it looks: the Oracle page warns that a date with no time defaults to midnight UTC,
    /// which shifts it to the previous day when it is read back in a Western Hemisphere
    /// zone — which is every zone this application runs in. A <see cref="DateOnly"/> has no
    /// time to shift, and the parsing below takes the date exactly as written and never
    /// converts a zone.
    /// </para>
    /// </remarks>
    internal static class SiebelDate
    {
        /// <summary>The format used when writing a <c>DTYPE_UTCDATETIME</c>.</summary>
        public const string UtcDateTimeFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

        /// <summary>The format used when writing a zone-less <c>DTYPE_DATETIME</c>.</summary>
        public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss";

        /// <summary>The format used when writing a <c>DTYPE_DATE</c>.</summary>
        public const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// Every shape the ISO grammar permits, with the optional parts present and absent.
        /// <c>FFFFFFF</c> covers "fractional seconds, or none"; <c>K</c> covers
        /// "<c>Z</c>, an offset, or nothing".
        /// </summary>
        private static readonly string[] DateTimeFormats =
        [
            "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
            "yyyy-MM-ddTHH:mmK",
            "yyyy-MM-dd HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd HH:mmK",
            DateFormat,
        ];

        /// <summary>Reads a <c>DTYPE_UTCDATETIME</c>.</summary>
        /// <param name="value">The raw field value.</param>
        /// <param name="field">The ICM field name, for the unparsed record.</param>
        /// <param name="unparsed">Collects values that could not be read.</param>
        /// <returns>The instant, or null when the field was empty or unreadable.</returns>
        public static DateTimeOffset? ToUtcDateTime(
            string? value, string field, IDictionary<string, string> unparsed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // A value of this type with no offset is UTC by definition, so it is assumed
            // rather than read as local time; one that carries its own offset keeps it,
            // which is what AdjustToUniversal preserves.
            if (DateTimeOffset.TryParseExact(
                    value.Trim(),
                    DateTimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                return parsed;
            }

            unparsed[field] = value;
            return null;
        }

        /// <summary>Reads a <c>DTYPE_DATETIME</c>, which carries no time zone.</summary>
        /// <param name="value">The raw field value.</param>
        /// <param name="field">The ICM field name, for the unparsed record.</param>
        /// <param name="unparsed">Collects values that could not be read.</param>
        /// <returns>
        /// The date and time as written, with <see cref="DateTimeKind.Unspecified"/>, or
        /// null when the field was empty or unreadable.
        /// </returns>
        public static DateTime? ToDateTime(
            string? value, string field, IDictionary<string, string> unparsed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // RoundtripKind so a value that does carry an offset is not converted into
            // local time behind our backs; the kind is then flattened, because this Siebel
            // type has no zone and reporting one would be inventing information.
            if (DateTime.TryParseExact(
                    value.Trim(),
                    DateTimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
            }

            unparsed[field] = value;
            return null;
        }

        /// <summary>Reads a <c>DTYPE_DATE</c>.</summary>
        /// <param name="value">The raw field value.</param>
        /// <param name="field">The ICM field name, for the unparsed record.</param>
        /// <param name="unparsed">Collects values that could not be read.</param>
        /// <returns>The date, or null when the field was empty or unreadable.</returns>
        /// <remarks>
        /// The grammar makes the month and the day optional on a Date, so <c>2026</c> and
        /// <c>2026-08</c> are legal values that a <see cref="DateOnly"/> cannot represent.
        /// They are reported as unreadable rather than being completed with a guessed month
        /// or first-of-the-month.
        /// </remarks>
        public static DateOnly? ToDate(
            string? value, string field, IDictionary<string, string> unparsed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();

            if (DateOnly.TryParseExact(
                    trimmed, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
            {
                return parsed;
            }

            // A date field arriving with a time on it is common enough to handle. The date
            // is taken exactly as written — no zone conversion — so a midnight-UTC value
            // cannot roll back a day on the way in.
            if (DateTime.TryParseExact(
                    trimmed,
                    DateTimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime withTime))
            {
                return DateOnly.FromDateTime(withTime);
            }

            unparsed[field] = value;
            return null;
        }

        /// <summary>Writes a zone-less <c>DTYPE_DATETIME</c>.</summary>
        /// <param name="value">The value, or null to leave the field alone.</param>
        /// <returns>The ISO 8601 value, or null to omit the field.</returns>
        /// <remarks>
        /// No offset is written. The grammar recommends one, but this Siebel type does not
        /// carry a zone, so any offset put here would be made up.
        /// </remarks>
        public static string? FromDateTime(DateTime? value) =>
            value?.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

        /// <summary>Writes a <c>DTYPE_UTCDATETIME</c>.</summary>
        /// <param name="value">The value, or null to leave the field alone.</param>
        /// <returns>The ISO 8601 value in UTC, or null to omit the field.</returns>
        /// <remarks>
        /// Converted to UTC and written with an explicit <c>Z</c>: the grammar recommends
        /// stating the offset, and a local time sent in a UTC field would be wrong by that
        /// offset with nothing to signal it. Sub-second precision is dropped, which no
        /// field on a service request has any use for.
        /// </remarks>
        public static string? FromUtcDateTime(DateTimeOffset? value) =>
            value?.UtcDateTime.ToString(UtcDateTimeFormat, CultureInfo.InvariantCulture);

        /// <summary>Writes a <c>DTYPE_DATE</c>.</summary>
        /// <param name="value">The value, or null to leave the field alone.</param>
        /// <returns>The ISO 8601 date, or null to omit the field.</returns>
        /// <remarks>
        /// A date and nothing else. The Oracle page's midnight-UTC warning is about a
        /// <i>DateTime</i> given without a time; a Date field is date-only by definition,
        /// and adding a time to it here is what would invite the shift.
        /// </remarks>
        public static string? FromDate(DateOnly? value) =>
            value?.ToString(DateFormat, CultureInfo.InvariantCulture);
    }
}
