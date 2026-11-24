namespace Myss.FormSpec.Tests
{
    using System.Text.Json;
    using Myss.FormSpec.Domain;
    using Myss.FormSpec.Tests.TestSupport;

    /// <summary>
    /// Tests for the channel-rule part of <see cref="FormSpecValidator.ValidateSpecStructure"/>.
    /// The cases are the publish cases of <c>Shared/validation/channel-specs.json</c>.
    /// </summary>
    public class ChannelRuleStructureTests
    {
        public static TheoryData<string> CaseNames() => ChannelCases.Names("publishCases");

        [Fact]
        public void TheSharedFile_HasCasesToRun()
        {
            Assert.NotEmpty(CaseNames());
        }

        [Theory]
        [MemberData(nameof(CaseNames))]
        public void ValidateSpecStructure_ReportsTheSharedCasesProblems(string name)
        {
            JsonElement item = ChannelCases.Case("publishCases", name);
            string[] expected = [.. item.GetProperty("problems").EnumerateArray().Select(p => p.GetString()!)];

            IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator.ValidateSpecStructure(item.GetProperty("spec"));

            Assert.Equal(expected, errors.Where(e => e.Keyword == FormSpecStructureKeywords.ChannelRulesInvalid).Select(e => e.Message));
            Assert.DoesNotContain(errors, e => e.Keyword != FormSpecStructureKeywords.ChannelRulesInvalid);
        }

        [Fact]
        public void ValidateSpecStructure_NamesTheComponentThatBreaksTheRule()
        {
            using JsonDocument spec = JsonDocument.Parse("""
                { "components": [
                  { "type": "radio", "key": "hasSpouse", "properties": { "myssChannels": { "client": { "show": false } } } },
                  { "type": "textfield", "key": "spouseName", "conditional": { "when": "hasSpouse" } },
                  { "type": "textfield", "key": "sin", "properties": { "myssChannels": { "client": "no" } } }
                ] }
                """);

            IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator.ValidateSpecStructure(spec.RootElement);

            Assert.Equal(["spouseName", "sin"], errors.Select(e => e.Field));
        }

        [Fact]
        public void ValidateSpecStructure_LeavesAKeylessComponentsRulesToTheKeyRule()
        {
            using JsonDocument spec = JsonDocument.Parse("""
                { "components": [ { "type": "textfield", "properties": { "myssChannels": { "nobody": true } } } ] }
                """);

            ValidationErrorModel error = Assert.Single(FormSpecValidator.ValidateSpecStructure(spec.RootElement));

            Assert.Equal(FormSpecStructureKeywords.ComponentKeyMissing, error.Keyword);
        }

        [Fact]
        public void TheKeyword_IsTrustedFromAStrapiRefusal()
        {
            Assert.Contains(FormSpecStructureKeywords.ChannelRulesInvalid, FormSpecStructureKeywords.LifecycleKeywords);
        }
    }
}
