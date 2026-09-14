namespace Myss.Api.Services
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    /// The BC Bus Pass form's component keys and the readers the rules and the
    /// mapper share, so both agree on what an answer is.
    /// </summary>
    /// <remarks>
    /// The keys must stay in sync with the <c>bc-bus-pass</c> spec seeded by
    /// MyssContent (<c>src/lib/bus-pass-form.json</c>).
    /// </remarks>
    public static class BusPassAnswers
    {
        /// <summary>Radio: "existing" or "new".</summary>
        public const string ApplicantCategory = "applicantCategory";

        /// <summary>Radio for existing clients: "moved" or "replacement".</summary>
        public const string ExistingClientReason = "existingClientReason";

        /// <summary>Checkbox for new applicants.</summary>
        public const string EligibilityAcknowledged = "eligibilityAcknowledged";

        /// <summary>Radio for new applicants: "over65", "firstNations" or "neither".</summary>
        public const string EligibilityCategory = "eligibilityCategory";

        /// <summary>Text: the social insurance number.</summary>
        public const string SocialInsuranceNumber = "socialInsuranceNumber";

        /// <summary>Text: the bus pass account number.</summary>
        public const string BusPassAccountNumber = "busPassAccountNumber";

        /// <summary>Text: the first name.</summary>
        public const string FirstName = "firstName";

        /// <summary>Text: the last name.</summary>
        public const string LastName = "lastName";

        /// <summary>Text: day of birth.</summary>
        public const string BirthDay = "birthDay";

        /// <summary>Select: month of birth, "01" to "12".</summary>
        public const string BirthMonth = "birthMonth";

        /// <summary>Text: year of birth.</summary>
        public const string BirthYear = "birthYear";

        /// <summary>Text: the phone number.</summary>
        public const string PhoneNumber = "phoneNumber";

        /// <summary>Select: "home", "work" or "cell".</summary>
        public const string PhoneType = "phoneType";

        /// <summary>Checkbox: a message may be left.</summary>
        public const string LeaveMessage = "leaveMessage";

        /// <summary>Email: the notification address.</summary>
        public const string Email = "email";

        /// <summary>Select: "phone" or "email".</summary>
        public const string PreferredCommunication = "preferredCommunication";

        /// <summary>Text: residential street address, line 1.</summary>
        public const string StreetAddress1 = "streetAddress1";

        /// <summary>Text: residential street address, line 2.</summary>
        public const string StreetAddress2 = "streetAddress2";

        /// <summary>Text: residential city.</summary>
        public const string City = "city";

        /// <summary>Text: residential province.</summary>
        public const string Province = "province";

        /// <summary>Text: residential postal code.</summary>
        public const string PostalCode = "postalCode";

        /// <summary>Radio: "yes" or "no".</summary>
        public const string MailingAddressDifferent = "mailingAddressDifferent";

        /// <summary>Text: mailing street address, line 1.</summary>
        public const string MailingStreetAddress1 = "mailingStreetAddress1";

        /// <summary>Text: mailing street address, line 2.</summary>
        public const string MailingStreetAddress2 = "mailingStreetAddress2";

        /// <summary>Text: mailing city.</summary>
        public const string MailingCity = "mailingCity";

        /// <summary>Text: mailing province.</summary>
        public const string MailingProvince = "mailingProvince";

        /// <summary>Text: mailing postal code.</summary>
        public const string MailingPostalCode = "mailingPostalCode";

        /// <summary>
        /// Reads a text answer, trimmed. Null when absent, blank, or not text.
        /// A number is accepted as its literal text, since the date parts may
        /// arrive either way.
        /// </summary>
        /// <param name="answers">The submitted answers.</param>
        /// <param name="key">The component key.</param>
        /// <returns>The trimmed text, or null.</returns>
        public static string? GetString(JsonElement answers, string key)
        {
            if (!answers.TryGetProperty(key, out JsonElement value))
            {
                return null;
            }

            string? text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        /// <summary>
        /// Reads a checkbox answer. Null when absent or not a boolean.
        /// </summary>
        /// <param name="answers">The submitted answers.</param>
        /// <param name="key">The component key.</param>
        /// <returns>The value, or null.</returns>
        public static bool? GetBool(JsonElement answers, string key)
        {
            if (!answers.TryGetProperty(key, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

        /// <summary>
        /// Keeps only the digits of a masked or formatted number. Null when
        /// nothing is left.
        /// </summary>
        /// <param name="raw">The value as typed.</param>
        /// <returns>The digits, or null.</returns>
        public static string? Digits(string? raw)
        {
            if (raw is null)
            {
                return null;
            }

            string digits = new(raw.Where(char.IsAsciiDigit).ToArray());
            return digits.Length == 0 ? null : digits;
        }

        /// <summary>
        /// Folds the three date-of-birth parts into a date.
        /// </summary>
        /// <param name="answers">The submitted answers.</param>
        /// <param name="dateOfBirth">The date, when the parts make one.</param>
        /// <returns>True when all three parts are present and form a real calendar date.</returns>
        public static bool TryGetDateOfBirth(JsonElement answers, out DateOnly dateOfBirth)
        {
            dateOfBirth = default;

            if (!TryGetInt(answers, BirthYear, out int year)
                || !TryGetInt(answers, BirthMonth, out int month)
                || !TryGetInt(answers, BirthDay, out int day))
            {
                return false;
            }

            if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                return false;
            }

            dateOfBirth = new DateOnly(year, month, day);
            return true;
        }

        /// <summary>
        /// Normalises the province the way the legacy form did: the spelled-out
        /// name becomes the two-letter code, anything else passes through.
        /// </summary>
        /// <param name="raw">The value as typed.</param>
        /// <returns>The normalised value.</returns>
        public static string? NormalizeProvince(string? raw)
        {
            return string.Equals(raw, "British Columbia", StringComparison.OrdinalIgnoreCase) ? "BC" : raw;
        }

        private static bool TryGetInt(JsonElement answers, string key, out int value)
        {
            return int.TryParse(GetString(answers, key), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
    }
}
