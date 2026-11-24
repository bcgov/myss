namespace Myss.FormSpec.Tests
{
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Myss.FormSpec.Domain;
    using Myss.FormSpec.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="ChannelSpec.Resolve"/>. The cases are the resolve cases of
    /// <c>Shared/validation/channel-specs.json</c>, which MyssFormio's preview also runs.
    /// </summary>
    public class ChannelSpecTests
    {
        public static TheoryData<string> CaseNames() => ChannelCases.Names("resolveCases");

        [Fact]
        public void TheSharedFile_HasCasesToRun()
        {
            Assert.NotEmpty(CaseNames());
        }

        [Theory]
        [MemberData(nameof(CaseNames))]
        public void Resolve_GivesTheSharedCasesFormForEveryChannel(string name)
        {
            JsonElement item = ChannelCases.Case("resolveCases", name);
            JsonElement resolved = item.GetProperty("resolved");
            Assert.Equal(RequestChannels.All.Order(), resolved.EnumerateObject().Select(p => p.Name).Order());

            foreach (string channel in RequestChannels.All)
            {
                JsonElement actual = ChannelSpec.Resolve(item.GetProperty("spec"), channel);

                Assert.True(
                    JsonNode.DeepEquals(JsonNode.Parse(resolved.GetProperty(channel).GetRawText()), JsonNode.Parse(actual.GetRawText())),
                    $"{channel}: {actual.GetRawText()}");
            }
        }

        [Theory]
        [MemberData(nameof(CaseNames))]
        public void Resolve_GivesAFormTheStructureRulesAccept(string name)
        {
            JsonElement spec = ChannelCases.Case("resolveCases", name).GetProperty("spec");
            Assert.Empty(FormSpecValidator.ValidateSpecStructure(spec));

            foreach (string channel in RequestChannels.All)
            {
                Assert.Empty(FormSpecValidator.ValidateSpecStructure(ChannelSpec.Resolve(spec, channel)));
            }
        }

        [Fact]
        public void Resolve_ReturnsAFormWithoutRulesAsItIs_ThatOutlivesItsDocument()
        {
            const string Text = """{ "components": [ { "type": "textfield", "key": "firstName" } ] }""";
            JsonElement resolved;
            using (JsonDocument spec = JsonDocument.Parse(Text))
            {
                resolved = ChannelSpec.Resolve(spec.RootElement, RequestChannels.Client);
            }

            Assert.Equal(Text, resolved.GetRawText());
        }

        [Fact]
        public void Resolve_RefusesAnUnknownChannel()
        {
            using JsonDocument spec = JsonDocument.Parse("""{ "components": [] }""");

            Assert.Throws<ArgumentOutOfRangeException>(() => ChannelSpec.Resolve(spec.RootElement, "applicant-override"));
        }

        [Fact]
        public void TheResolvedSin_IsRequiredForAClientAndOptionalForAWorker()
        {
            JsonElement spec = ChannelCases.Case("resolveCases", "the SIN: required for a client, optional for a worker").GetProperty("spec");
            using JsonDocument noAnswers = JsonDocument.Parse("{}");

            IReadOnlyList<ValidationErrorModel> client = FormSpecValidator.Validate(ChannelSpec.Resolve(spec, RequestChannels.Client), noAnswers.RootElement);
            IReadOnlyList<ValidationErrorModel> worker = FormSpecValidator.Validate(ChannelSpec.Resolve(spec, RequestChannels.WorkerOverride), noAnswers.RootElement);

            Assert.Equal(ValidationKeywords.FieldRequired, Assert.Single(client).Keyword);
            Assert.Empty(worker);
        }

        [Fact]
        public void AnAnswerToAFieldHiddenInTheChannel_IsAnUnknownKey()
        {
            JsonElement spec = ChannelCases.Case("resolveCases", "a field shown to a worker only").GetProperty("spec");
            using JsonDocument answers = JsonDocument.Parse("""{ "firstName": "Ada", "workerNote": "Called in." }""");

            IReadOnlyList<ValidationErrorModel> client = FormSpecValidator.Validate(ChannelSpec.Resolve(spec, RequestChannels.Client), answers.RootElement);
            IReadOnlyList<ValidationErrorModel> worker = FormSpecValidator.Validate(ChannelSpec.Resolve(spec, RequestChannels.WorkerOverride), answers.RootElement);

            ValidationErrorModel unknown = Assert.Single(client);
            Assert.Equal(("workerNote", ValidationKeywords.FieldUnknown), (unknown.Field, unknown.Keyword));
            Assert.Empty(worker);
        }
    }
}
