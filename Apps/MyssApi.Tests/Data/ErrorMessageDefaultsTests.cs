namespace Myss.Api.Tests.Data
{
    using System.Reflection;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Models;

    /// <summary>
    /// Keeps <see cref="ErrorMessageDefaults"/> identical to the shared catalogue
    /// in <c>Shared/validation/error-messages.json</c>, and the catalogue complete
    /// for every keyword the code can raise.
    /// </summary>
    /// <remarks>
    /// The shared file is linked into this project by the csproj and copied
    /// beside the test assembly, the way the validation vectors are. If it
    /// cannot be found that is a build configuration failure, and the suite says
    /// so rather than silently comparing against nothing.
    /// </remarks>
    public partial class ErrorMessageDefaultsTests
    {
        /// <summary>
        /// Keywords the catalogue deliberately leaves to code, and why:
        /// per-field wording (required), a value embedded in the message (the
        /// unknown key, the wrong-typed key, the claimed version), or one keyword
        /// worded two ways by the browser (the ministry outage). The shared file's
        /// header is the authority; this list must agree with it.
        /// </summary>
        private static readonly HashSet<string> CodeOnlyKeywords =
        [
            ValidationKeywords.FieldRequired,
            ValidationKeywords.FieldUnknown,
            ValidationKeywords.FieldWrongType,
            ValidationKeywords.VersionUnknown,
            BusPassErrorKeywords.IcmUnavailable,
        ];

        [Fact]
        public void Defaults_MatchTheSharedCatalogue_RowForRow()
        {
            Dictionary<string, string> shared = ReadSharedCatalogue();

            // Both directions: a row missing from either side is a divergence.
            Assert.Equal(shared.Count, ErrorMessageDefaults.Messages.Count);
            foreach ((string keyword, string message) in shared)
            {
                Assert.True(
                    ErrorMessageDefaults.Messages.TryGetValue(keyword, out string? compiled),
                    $"The shared catalogue has {keyword} but ErrorMessageDefaults does not.");
                Assert.Equal(message, compiled);
            }
        }

        [Fact]
        public void Defaults_UseDomainContextNameKeywords()
        {
            foreach (string keyword in ErrorMessageDefaults.Messages.Keys)
            {
                Assert.Matches(KeywordPattern(), keyword);
            }
        }

        [Fact]
        public void Defaults_NeverCarryABlankMessage()
        {
            foreach ((string keyword, string message) in ErrorMessageDefaults.Messages)
            {
                Assert.False(string.IsNullOrWhiteSpace(message), $"{keyword} has a blank default message.");
            }
        }

        [Fact]
        public void Defaults_LeaveRequiredToPerFieldWording()
        {
            // A catalogue row for FORM.FIELD.REQUIRED would flatten every
            // field-specific required message in BusPassRules and the specs to
            // one sentence; the shared file's header records the decision.
            Assert.DoesNotContain(ValidationKeywords.FieldRequired, ErrorMessageDefaults.Messages.Keys);
        }

        [Theory]
        [InlineData(typeof(ValidationKeywords))]
        [InlineData(typeof(BusPassErrorKeywords))]
        public void EveryKeywordTheCodeRaises_HasWording_OrIsListedAsCodeOnly(Type keywordClass)
        {
            // A new keyword cannot ship without wording: either it gets a
            // catalogue row (in the shared file and here) or it is added to
            // CodeOnlyKeywords with a reason in the shared file's header.
            IEnumerable<string> keywords = keywordClass
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!);

            foreach (string keyword in keywords)
            {
                Assert.True(
                    ErrorMessageDefaults.Messages.ContainsKey(keyword) || CodeOnlyKeywords.Contains(keyword),
                    $"{keywordClass.Name}.{keyword} has no catalogue wording and is not listed as code-only.");
            }
        }

        [Fact]
        public void CodeOnlyKeywords_AreNotAlsoInTheCatalogue()
        {
            foreach (string keyword in CodeOnlyKeywords)
            {
                Assert.DoesNotContain(keyword, ErrorMessageDefaults.Messages.Keys);
            }
        }

        [GeneratedRegex("^[A-Z0-9_]+(\\.[A-Z0-9_]+)+$")]
        private static partial Regex KeywordPattern();

        private static Dictionary<string, string> ReadSharedCatalogue()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "error-messages.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The shared error message catalogue was not copied beside the test assembly. " +
                    "Check the <Content Include=\"..\\..\\Shared\\validation\\error-messages.json\"> " +
                    "item in MyssApi.Tests.csproj.",
                    path);
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            var catalogue = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonElement row in document.RootElement.GetProperty("messages").EnumerateArray())
            {
                string keyword = row.GetProperty("keyword").GetString()!;
                Assert.False(catalogue.ContainsKey(keyword), $"The shared catalogue lists {keyword} twice.");
                catalogue[keyword] = row.GetProperty("message").GetString()!;
            }

            return catalogue;
        }
    }
}
