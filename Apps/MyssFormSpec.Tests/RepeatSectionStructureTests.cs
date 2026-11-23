namespace Myss.FormSpec.Tests
{
    using System.Text.Json;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Tests for the repeat-section rule of
    /// <see cref="FormSpecValidator.ValidateSpecStructure"/>. The cases come from
    /// <c>Shared/validation/repeat-section-specs.json</c>, which MyssContent's
    /// publish rule and MyssFormio's editor check also run, so the three copies
    /// word every problem alike.
    /// </summary>
    public class RepeatSectionStructureTests
    {
        private static readonly JsonDocument Shared = Load();

        public static TheoryData<string> CaseNames()
        {
            TheoryData<string> names = [];
            foreach (JsonElement item in Shared.RootElement.GetProperty("cases").EnumerateArray())
            {
                names.Add(item.GetProperty("name").GetString()!);
            }

            return names;
        }

        [Fact]
        public void TheSharedFile_HasCasesToRun()
        {
            Assert.NotEmpty(CaseNames());
        }

        [Theory]
        [MemberData(nameof(CaseNames))]
        public void ValidateSpecStructure_ReportsTheSharedCasesProblems(string name)
        {
            JsonElement item = Shared.RootElement.GetProperty("cases").EnumerateArray()
                .Single(c => c.GetProperty("name").GetString() == name);
            string[] expected = [.. item.GetProperty("problems").EnumerateArray().Select(p => p.GetString()!)];

            IReadOnlyList<ValidationErrorModel> errors = FormSpecValidator.ValidateSpecStructure(item.GetProperty("spec"));

            Assert.Equal(expected, errors.Where(e => e.Keyword == FormSpecStructureKeywords.RepeatSectionInvalid).Select(e => e.Message));
            Assert.DoesNotContain(errors, e => e.Keyword != FormSpecStructureKeywords.RepeatSectionInvalid);
        }

        [Fact]
        public void ValidateSpecStructure_NamesTheSectionAsTheField()
        {
            using JsonDocument spec = JsonDocument.Parse("""
                { "components": [ { "type": "bcgovRepeatSection", "key": "children", "components": [ { "key": "a" } ] } ] }
                """);

            ValidationErrorModel error = Assert.Single(FormSpecValidator.ValidateSpecStructure(spec.RootElement));

            Assert.Equal("children", error.Field);
        }

        [Fact]
        public void TheKeyword_IsTrustedFromAStrapiRefusal()
        {
            Assert.Contains(FormSpecStructureKeywords.RepeatSectionInvalid, FormSpecStructureKeywords.LifecycleKeywords);
        }

        private static JsonDocument Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "repeat-section-specs.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The shared repeat-section cases were not copied beside the test assembly. Check the "
                    + "<Content Include=\"..\\..\\Shared\\validation\\repeat-section-specs.json\"> item in the csproj.",
                    path);
            }

            return JsonDocument.Parse(File.ReadAllText(path));
        }
    }
}
