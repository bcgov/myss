namespace Myss.Api.Domain
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A calendar date as a form field stores it: an ISO <c>yyyy-MM-dd</c>, or a
    /// datetime whose date portion is what counts.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="DateOnly.TryParse(string, out DateOnly)"/>:
    /// that accepts culture-shaped text such as "10/12/1950", which the
    /// browser's <c>date</c> rule refuses, and the two sides must agree. Both
    /// are driven by the <c>date</c> vectors in
    /// <c>Shared/validation/validation-vectors.json</c>.
    /// </remarks>
    public static partial class IsoDate
    {
        /// <summary>Parses the date portion of an ISO date or datetime.</summary>
        /// <param name="raw">The value as submitted.</param>
        /// <param name="date">The date, when the text is a real one.</param>
        /// <returns>True when the text is a real ISO date.</returns>
        public static bool TryParse(string? raw, out DateOnly date)
        {
            date = default;
            Match match = Pattern().Match((raw ?? string.Empty).Trim());
            if (!match.Success)
            {
                return false;
            }

            int year = int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
            int month = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
            int day = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
            return TryCreate(year, month, day, out date);
        }

        /// <summary>Builds a date from its parts when they make a real one.</summary>
        /// <param name="year">The year, 1 to 9999.</param>
        /// <param name="month">The month, 1 to 12.</param>
        /// <param name="day">The day of the month.</param>
        /// <param name="date">The date, when the parts make a real one.</param>
        /// <returns>True when the parts make a real date.</returns>
        public static bool TryCreate(int year, int month, int day, out DateOnly date)
        {
            date = default;
            if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                return false;
            }

            date = new DateOnly(year, month, day);
            return true;
        }

        // [0-9], not \d: in .NET \d also matches other scripts' digits, which
        // int.Parse then refuses, and which the browser's rule never accepts.
        [GeneratedRegex(@"^(?<y>[0-9]{4})-(?<m>[0-9]{2})-(?<d>[0-9]{2})(?:$|T)", RegexOptions.CultureInvariant)]
        private static partial Regex Pattern();
    }
}
