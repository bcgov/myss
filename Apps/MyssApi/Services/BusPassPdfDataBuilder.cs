namespace Myss.Api.Services
{
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Builds the PDF template data object for a BC Bus Pass submission, using
    /// <see cref="BusPassPdfFieldMap"/> as its config.
    /// </summary>
    public static class BusPassPdfDataBuilder
    {
        /// <summary>
        /// Maps a submission's answers to the data Carbone will merge into the
        /// bus pass ODT template.
        /// </summary>
        /// <param name="answers">The submitted answers, keyed by component key.</param>
        /// <returns>The template data object.</returns>
        public static Dictionary<string, object?> Build(JsonElement answers)
        {
            var data = new Dictionary<string, object?>();

            foreach ((string answerKey, string templateKey) in BusPassPdfFieldMap.PassthroughFields)
            {
                data[templateKey] = ReadRaw(answers, answerKey);
            }

            foreach ((string key, IReadOnlyDictionary<string, string> labels) in BusPassPdfFieldMap.CodedValueLabels)
            {
                data[key] = ResolveLabel(answers, key, labels);
            }

            data["dateOfBirth"] = BuildDateOfBirth(answers);
            ApplyMailingAddress(answers, data);

            return data;
        }

        private static string BuildDateOfBirth(JsonElement answers)
        {
            string day = GetString(answers, "birthDay") ?? string.Empty;
            string year = GetString(answers, "birthYear") ?? string.Empty;
            string monthCode = GetString(answers, "birthMonth") ?? string.Empty;
            string month = BusPassPdfFieldMap.BirthMonthLabels.GetValueOrDefault(monthCode, monthCode);

            return $"{day} {month} {year}".Trim();
        }

        /// <summary>
        /// The mailing address fields are only collected when the citizen says
        /// their mailing address differs; otherwise the residential address is
        /// what gets mailed to.
        /// </summary>
        private static void ApplyMailingAddress(JsonElement answers, Dictionary<string, object?> data)
        {
            bool hasSeparateMailingAddress = GetString(answers, "mailingAddressDifferent") == "yes";
            if (hasSeparateMailingAddress)
            {
                return;
            }

            data["mailingStreetAddress1"] = ReadRaw(answers, "streetAddress1");
            data["mailingStreetAddress2"] = ReadRaw(answers, "streetAddress2");
            data["mailingCity"] = ReadRaw(answers, "city");
            data["mailingProvince"] = ReadRaw(answers, "province");
            data["mailingPostalCode"] = ReadRaw(answers, "postalCode");
        }

        /// <summary>
        /// Resolves a coded radio/select answer to its human-readable label.
        /// Falls back to the raw code when it is not in the lookup, so an
        /// unmapped value is still visible on the PDF rather than silently blank.
        /// </summary>
        private static string? ResolveLabel(JsonElement answers, string key, IReadOnlyDictionary<string, string> labels)
        {
            string? raw = GetString(answers, key);
            if (raw is null)
            {
                return null;
            }

            return labels.GetValueOrDefault(raw, raw);
        }

        private static object? ReadRaw(JsonElement answers, string key)
        {
            if (!answers.TryGetProperty(key, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.Null => null,
                _ => null,
            };
        }

        private static string? GetString(JsonElement answers, string key)
        {
            return answers.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
