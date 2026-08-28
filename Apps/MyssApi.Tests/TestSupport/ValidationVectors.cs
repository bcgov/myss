namespace Myss.Api.Tests.TestSupport
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Reads <c>Shared/validation/validation-vectors.json</c>, the file that
    /// keeps the C# and TypeScript implementations of the same rules honest.
    /// </summary>
    /// <remarks>
    /// The file is linked into this project by the csproj and copied beside the
    /// test assembly. If it cannot be found, that is a build configuration
    /// failure and the suite says so loudly rather than silently testing
    /// nothing — a fixture-driven suite that quietly finds zero cases is the
    /// worst possible outcome, because it stays green while proving nothing.
    /// </remarks>
    public static class ValidationVectors
    {
        private static readonly JsonDocument Document = Load();

        /// <summary>A case: the input, and the keyword expected when it fails.</summary>
        /// <param name="Value">The input value.</param>
        /// <param name="Keyword">The expected failure keyword, if any.</param>
        /// <param name="Note">The note recorded in the fixture, for test output.</param>
        public record Vector(string Value, string? Keyword, string? Note);

        /// <summary>A confirmation-match case.</summary>
        /// <param name="Value">The address.</param>
        /// <param name="Confirmation">The confirmation field's value.</param>
        /// <param name="Keyword">The expected failure keyword, if any.</param>
        public record ConfirmationVector(string Value, string Confirmation, string? Keyword);

        /// <summary>Gets the valid cases for a rule.</summary>
        /// <param name="rule">The fixture section, e.g. "sin".</param>
        /// <returns>Every valid case.</returns>
        public static IReadOnlyList<Vector> Valid(string rule) => Read(rule, "valid");

        /// <summary>Gets the invalid cases for a rule.</summary>
        /// <param name="rule">The fixture section, e.g. "sin".</param>
        /// <returns>Every invalid case.</returns>
        public static IReadOnlyList<Vector> Invalid(string rule) => Read(rule, "invalid");

        /// <summary>Gets the confirmation cases.</summary>
        /// <param name="section">Either "matching" or "mismatching".</param>
        /// <returns>Every case in that section.</returns>
        public static IReadOnlyList<ConfirmationVector> Confirmations(string section)
        {
            List<ConfirmationVector> vectors = [];
            foreach (JsonElement item in Document.RootElement
                .GetProperty("emailConfirmation").GetProperty(section).EnumerateArray())
            {
                vectors.Add(new ConfirmationVector(
                    item.GetProperty("value").GetString()!,
                    item.GetProperty("confirmation").GetString()!,
                    item.TryGetProperty("keyword", out JsonElement k) ? k.GetString() : null));
            }

            return vectors;
        }

        /// <summary>Wraps the vectors as xUnit theory data.</summary>
        /// <param name="vectors">The cases.</param>
        /// <returns>One object array per case.</returns>
        public static IEnumerable<object[]> AsTheoryData(IReadOnlyList<Vector> vectors)
        {
            foreach (Vector vector in vectors)
            {
                yield return [vector.Value, vector.Keyword ?? string.Empty];
            }
        }

        private static IReadOnlyList<Vector> Read(string rule, string section)
        {
            List<Vector> vectors = [];
            foreach (JsonElement item in Document.RootElement.GetProperty(rule).GetProperty(section).EnumerateArray())
            {
                vectors.Add(new Vector(
                    item.GetProperty("value").GetString()!,
                    item.TryGetProperty("keyword", out JsonElement k) ? k.GetString() : null,
                    item.TryGetProperty("note", out JsonElement n) ? n.GetString() : null));
            }

            return vectors;
        }

        private static JsonDocument Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "validation-vectors.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The shared validation vectors were not copied beside the test assembly. " +
                    "Check the <Content Include=\"..\\..\\Shared\\validation\\validation-vectors.json\"> " +
                    "item in MyssApi.Tests.csproj.",
                    path);
            }

            return JsonDocument.Parse(File.ReadAllText(path));
        }
    }
}
