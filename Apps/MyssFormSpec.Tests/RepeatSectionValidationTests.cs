namespace Myss.FormSpec.Tests
{
    using System.Text.Json;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Tests for <see cref="FormSpecValidator"/> on a repeat section: a group of
    /// questions answered once per unit of a count answer, submitted as a list
    /// of rows under the section's key.
    /// </summary>
    public class RepeatSectionValidationTests
    {
        private const string ChildrenSpec = """
        {
          "display": "form",
          "components": [
            { "type": "radio", "key": "hasChildren", "input": true, "validate": { "required": true } },
            { "type": "number", "key": "childCount", "input": true,
              "validate": { "required": true, "min": 1, "max": 10 },
              "conditional": { "show": true, "when": "hasChildren", "eq": "yes" } },
            { "type": "bcgovRepeatSection", "key": "children", "label": "Child Info", "input": true,
              "properties": { "myssRepeatCount": "childCount", "myssRepeatMax": "10", "myssRowLabel": "Child Info" },
              "conditional": { "show": true, "when": "hasChildren", "eq": "yes" },
              "components": [
                { "type": "textfield", "key": "childFirstName", "input": true, "validate": { "required": true } },
                { "type": "textfield", "key": "childLastName", "input": true, "validate": { "required": true } },
                { "type": "datetime", "key": "childDateOfBirth", "input": true,
                  "validate": { "required": true }, "properties": { "myssValidator": "date" } }
              ] },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private const string Child = """{"childFirstName":"Ada","childLastName":"Byron","childDateOfBirth":"2015-04-02"}""";

        [Fact]
        public void Validate_AcceptsOneCompleteRowPerChild()
        {
            Assert.Empty(Run(ChildrenSpec, Answers(3, Rows(3))));
        }

        [Fact]
        public void Validate_RefusesRowsThatDoNotNumberTheCount()
        {
            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, Answers(3, Rows(2))));

            Assert.Equal("children", error.Field);
            Assert.Equal(ValidationKeywords.FieldRowCountMismatch, error.Keyword);
        }

        [Fact]
        public void Validate_RefusesACountWithNoRows()
        {
            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, """{"hasChildren":"yes","childCount":2}"""));

            Assert.Equal("children", error.Field);
            Assert.Equal(ValidationKeywords.FieldRowCountMismatch, error.Keyword);
        }

        [Fact]
        public void Validate_ReadsAFractionalCountAsNoCount()
        {
            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, Answers("2.5", Rows(2))));

            Assert.Equal(ValidationKeywords.FieldRowCountMismatch, error.Keyword);
        }

        [Fact]
        public void Validate_LeavesAMissingCountToItsOwnRequiredRule()
        {
            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, """{"hasChildren":"yes","children":[]}"""));

            Assert.Equal("childCount", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void Validate_NamesARowFailureByItsPath()
        {
            string rows = $$"""[{{Child}},{"childLastName":"Byron","childDateOfBirth":"2017-09-30"}]""";

            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, Answers(2, rows)));

            Assert.Equal("children[1].childFirstName", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void Validate_AppliesARowFieldsDomainRule()
        {
            const string rows = """[{"childFirstName":"Ada","childLastName":"Byron","childDateOfBirth":"2015-02-30"}]""";

            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, Answers(1, rows)));

            Assert.Equal("children[0].childDateOfBirth", error.Field);
            Assert.Equal(ValidationKeywords.DateInvalid, error.Keyword);
        }

        [Fact]
        public void Validate_RefusesAnUnknownKeyInARow()
        {
            const string rows = """[{"childFirstName":"Ada","childLastName":"Byron","childDateOfBirth":"2015-04-02","nickname":"A"}]""";

            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, Answers(1, rows)));

            Assert.Equal("children[0].nickname", error.Field);
            Assert.Equal(ValidationKeywords.FieldUnknown, error.Keyword);
        }

        [Fact]
        public void Validate_RefusesARowFieldAnsweredOutsideItsRow()
        {
            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, """{"hasChildren":"no","childFirstName":"Ada"}"""));

            Assert.Equal("childFirstName", error.Field);
            Assert.Equal(ValidationKeywords.FieldUnknown, error.Keyword);
        }

        [Theory]
        [InlineData("""{"childFirstName":"Ada"}""")]
        [InlineData("""["Ada"]""")]
        [InlineData("\"3\"")]
        public void Validate_RefusesAnAnswerThatIsNotAListOfRows(string children)
        {
            ValidationErrorModel error = Assert.Single(Run(ChildrenSpec, Answers(1, children)));

            Assert.Equal("children", error.Field);
            Assert.Equal(ValidationKeywords.FieldWrongType, error.Keyword);
        }

        [Fact]
        public void Validate_RefusesMoreRowsThanTheMaximum_EvenWhenTheCountAgrees()
        {
            string spec = ChildrenSpec.Replace("\"myssRepeatMax\": \"10\"", "\"myssRepeatMax\": \"3\"", StringComparison.Ordinal);

            Assert.Empty(Run(spec, Answers(3, Rows(3))));
            ValidationErrorModel error = Assert.Single(Run(spec, Answers(4, Rows(4))));

            Assert.Equal("children", error.Field);
            Assert.Equal(ValidationKeywords.FieldWrongType, error.Keyword);
        }

        [Fact]
        public void Validate_AllowsTenRowsWhenTheSpecSetsNoMaximum()
        {
            string spec = ChildrenSpec.Replace("\"myssRepeatMax\": \"10\", ", string.Empty, StringComparison.Ordinal);

            Assert.Empty(Run(spec, Answers(10, Rows(10))));
            Assert.Equal(ValidationKeywords.FieldWrongType, Assert.Single(Run(spec, Answers(10, Rows(11)))).Keyword);
        }

        [Fact]
        public void Validate_CapsAnAuthoredMaximumAtFifty()
        {
            string spec = ChildrenSpec
                .Replace("\"myssRepeatMax\": \"10\"", "\"myssRepeatMax\": \"80\"", StringComparison.Ordinal)
                .Replace("\"min\": 1, \"max\": 10", "\"min\": 1", StringComparison.Ordinal);

            Assert.Empty(Run(spec, Answers(50, Rows(50))));
            Assert.Equal(ValidationKeywords.FieldWrongType, Assert.Single(Run(spec, Answers(51, Rows(51)))).Keyword);
        }

        [Fact]
        public void Validate_SkipsTheSection_WhenItsConditionHidesIt()
        {
            Assert.Empty(Run(ChildrenSpec, """{"hasChildren":"no"}"""));
        }

        [Fact]
        public void Validate_HoldsAHiddenSectionsRowsToTheirFormatButNotToRequired()
        {
            Assert.Empty(Run(ChildrenSpec, """{"hasChildren":"no","children":[{"childFirstName":""}]}"""));

            ValidationErrorModel error = Assert.Single(Run(
                ChildrenSpec,
                """{"hasChildren":"no","children":[{"childDateOfBirth":"2015-02-30"}]}"""));
            Assert.Equal("children[0].childDateOfBirth", error.Field);
            Assert.Equal(ValidationKeywords.DateInvalid, error.Keyword);
        }

        [Fact]
        public void Validate_ReadsARowConditionFromTheSameRow()
        {
            const string spec = """
            {
              "components": [
                { "type": "number", "key": "childCount", "input": true },
                { "type": "bcgovRepeatSection", "key": "children", "input": true,
                  "properties": { "myssRepeatCount": "childCount" },
                  "components": [
                    { "type": "radio", "key": "sharesLastName", "input": true },
                    { "type": "textfield", "key": "childLastName", "input": true,
                      "validate": { "required": true },
                      "conditional": { "show": true, "when": "sharesLastName", "eq": "no" } }
                  ] }
              ]
            }
            """;

            ValidationErrorModel error = Assert.Single(Run(
                spec,
                """{"childCount":2,"children":[{"sharesLastName":"yes"},{"sharesLastName":"no"}]}"""));

            Assert.Equal("children[1].childLastName", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void Validate_ReadsARowConditionFromOutsideTheSection()
        {
            const string spec = """
            {
              "components": [
                { "type": "radio", "key": "askDatesOfBirth", "input": true },
                { "type": "number", "key": "childCount", "input": true },
                { "type": "bcgovRepeatSection", "key": "children", "input": true,
                  "properties": { "myssRepeatCount": "childCount" },
                  "components": [
                    { "type": "datetime", "key": "childDateOfBirth", "input": true,
                      "validate": { "required": true },
                      "conditional": { "show": true, "when": "askDatesOfBirth", "eq": "yes" } }
                  ] }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"askDatesOfBirth":"no","childCount":1,"children":[{}]}"""));
            Assert.Equal(
                "children[0].childDateOfBirth",
                Assert.Single(Run(spec, """{"askDatesOfBirth":"yes","childCount":1,"children":[{}]}""")).Field);
        }

        [Fact]
        public void Validate_UsesTheSectionsAuthoredWordingForAMismatch()
        {
            string spec = ChildrenSpec.Replace(
                "\"label\": \"Child Info\",",
                "\"label\": \"Child Info\", \"errors\": { \"rowCount\": \"Add one Child Info for each child.\" },",
                StringComparison.Ordinal);

            ValidationErrorModel error = Assert.Single(Run(spec, Answers(2, Rows(1))));

            Assert.Equal("Add one Child Info for each child.", error.Message);
            Assert.True(error.HasAuthoredMessage);
        }

        private static string Rows(int count) => $"[{string.Join(",", Enumerable.Repeat(Child, count))}]";

        private static string Answers(object childCount, string children) =>
            $$"""{"hasChildren":"yes","childCount":{{childCount}},"children":{{children}}}""";

        private static IReadOnlyList<ValidationErrorModel> Run(string specJson, string answersJson)
        {
            using JsonDocument spec = JsonDocument.Parse(specJson);
            using JsonDocument answers = JsonDocument.Parse(answersJson);
            return FormSpecValidator.Validate(spec.RootElement, answers.RootElement);
        }
    }
}
