namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="FormSpecValidator"/> — structural validation of a
    /// submission against the spec version it claims.
    /// </summary>
    public class FormSpecValidatorTests
    {
        private const string SimpleSpec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "lastName", "input": true },
            { "type": "number", "key": "monthlyIncome", "input": true },
            { "type": "checkbox", "key": "declaration", "input": true },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        [Fact]
        public void Validate_AcceptsAWellFormedSubmission()
        {
            Assert.Empty(Run(SimpleSpec, """{"firstName":"Ada","monthlyIncome":2000,"declaration":true}"""));
        }

        [Fact]
        public void Validate_RejectsAnAnswerTheSpecHasNoFieldFor()
        {
            IReadOnlyList<ValidationErrorModel> errors =
                Run(SimpleSpec, """{"firstName":"Ada","isAdmin":true}""");

            ValidationErrorModel error = Assert.Single(errors);
            Assert.Equal("isAdmin", error.Field);
            Assert.Equal(ValidationKeywords.FieldUnknown, error.Keyword);
        }

        [Fact]
        public void Validate_DoesNotTreatTheSubmitButtonAsAnUnknownField()
        {
            // Form.io gives a submit button input:true like any field, so this
            // is a real trap: without excluding non-data types, every form
            // reports its own button as an unknown key.
            Assert.Empty(Run(SimpleSpec, """{"firstName":"Ada","submit":true}"""));
        }

        [Fact]
        public void Validate_RequiresFieldsTheSpecMarksRequired()
        {
            ValidationErrorModel error = Assert.Single(Run(SimpleSpec, """{"lastName":"Lovelace"}"""));

            Assert.Equal("firstName", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Theory]
        [InlineData("""{"firstName":""}""")]
        [InlineData("""{"firstName":"   "}""")]
        [InlineData("""{"firstName":null}""")]
        public void Validate_TreatsBlankAndNullAsMissing(string answers)
        {
            ValidationErrorModel error = Assert.Single(Run(SimpleSpec, answers));

            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Theory]
        [InlineData("""{"firstName":"Ada","monthlyIncome":"2000"}""", "monthlyIncome")]
        [InlineData("""{"firstName":123}""", "firstName")]
        [InlineData("""{"firstName":"Ada","declaration":"yes"}""", "declaration")]
        public void Validate_RejectsAnswersOfTheWrongJsonType(string answers, string expectedField)
        {
            ValidationErrorModel error = Assert.Single(Run(SimpleSpec, answers));

            Assert.Equal(expectedField, error.Field);
            Assert.Equal(ValidationKeywords.FieldWrongType, error.Keyword);
        }

        [Fact]
        public void Validate_FindsFieldsNestedInsidePanelsColumnsAndTables()
        {
            // A top-level scan would miss most of a real form and wrongly report
            // every nested answer as an unknown key.
            const string nested = """
            {
              "components": [
                { "type": "panel", "key": "about", "components": [
                  { "type": "textfield", "key": "inPanel", "input": true }
                ]},
                { "type": "columns", "key": "cols", "columns": [
                  { "components": [ { "type": "textfield", "key": "inColumn", "input": true } ] }
                ]},
                { "type": "table", "key": "grid", "rows": [
                  [ { "components": [ { "type": "textfield", "key": "inCell", "input": true } ] } ]
                ]}
              ]
            }
            """;

            Assert.Empty(Run(nested, """{"inPanel":"a","inColumn":"b","inCell":"c"}"""));
        }

        [Fact]
        public void Validate_ExemptsConditionalFieldsFromTheRequiredCheck()
        {
            // KNOWN GAP, deliberate: whether a conditional field is required
            // depends on other answers, and evaluating Form.io conditional logic
            // server-side is out of scope for this slice. Documented in
            // FormSpecValidator's remarks; must be closed before Phase 2's form.
            const string conditional = """
            {
              "components": [
                { "type": "select", "key": "relationship", "input": true },
                { "type": "textfield", "key": "spouseName", "input": true,
                  "validate": { "required": true },
                  "conditional": { "show": true, "when": "relationship", "eq": "couple" } }
              ]
            }
            """;

            Assert.Empty(Run(conditional, """{"relationship":"single"}"""));
        }

        [Fact]
        public void Validate_AppliesSinRules_WhenAFieldOptsInViaProperties()
        {
            // The marker route: a stock textfield opts in through Form.io's
            // free-form properties map, so a SIN can be validated server-side
            // before the Phase 1 custom component exists.
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "sin", "input": true,
                  "properties": { "myssValidator": "sin" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"sin":"050082833"}"""));

            ValidationErrorModel error = Assert.Single(Run(spec, """{"sin":"050082830"}"""));
            Assert.Equal("sin", error.Field);
            Assert.Equal(ValidationKeywords.SinInvalidChecksum, error.Keyword);
        }

        [Fact]
        public void Validate_AppliesSinRules_WhenTheComponentTypeIsSin()
        {
            // The type route: how the Phase 1 custom component will declare
            // itself. Both routes must reach the same rule.
            const string spec = """{ "components": [ { "type": "sin", "key": "sin", "input": true } ] }""";

            Assert.Equal(
                ValidationKeywords.SinWrongLength,
                Assert.Single(Run(spec, """{"sin":"12345"}""")).Keyword);
        }

        [Fact]
        public void Validate_AppliesEmailFormatRules()
        {
            const string spec = """{ "components": [ { "type": "email", "key": "contactEmail", "input": true } ] }""";

            Assert.Empty(Run(spec, """{"contactEmail":"ada@example.com"}"""));
            Assert.Equal(
                ValidationKeywords.EmailInvalidFormat,
                Assert.Single(Run(spec, """{"contactEmail":"ada@example"}""")).Keyword);
        }

        [Fact]
        public void Validate_EnforcesEmailConfirmation_ReportingAgainstTheConfirmationField()
        {
            const string spec = """
            {
              "components": [
                { "type": "email", "key": "contactEmail", "input": true },
                { "type": "email", "key": "confirmEmail", "input": true,
                  "properties": { "myssMatches": "contactEmail" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"contactEmail":"ada@example.com","confirmEmail":"ada@example.com"}"""));

            ValidationErrorModel error = Assert.Single(
                Run(spec, """{"contactEmail":"ada@example.com","confirmEmail":"ada@exampel.com"}"""));

            // The citizen's focus belongs on the field they must retype.
            Assert.Equal("confirmEmail", error.Field);
            Assert.Equal(ValidationKeywords.EmailMismatch, error.Keyword);
        }

        [Fact]
        public void Validate_ReportsEveryFailure_NotJustTheFirst()
        {
            // A citizen should see every problem on the page at once, which is
            // also what the WCAG error-summary pattern requires.
            IReadOnlyList<ValidationErrorModel> errors =
                Run(SimpleSpec, """{"monthlyIncome":"lots","unknownField":1}""");

            Assert.Equal(3, errors.Count);
            Assert.Contains(errors, e => e.Keyword == ValidationKeywords.FieldRequired);
            Assert.Contains(errors, e => e.Keyword == ValidationKeywords.FieldWrongType);
            Assert.Contains(errors, e => e.Keyword == ValidationKeywords.FieldUnknown);
        }

        [Fact]
        public void Validate_RejectsAnAnswersPayloadThatIsNotAnObject()
        {
            Assert.Equal(
                ValidationKeywords.FieldWrongType,
                Assert.Single(Run(SimpleSpec, "[]")).Keyword);
        }

        [Fact]
        public void Validate_EveryErrorCarriesAFieldKeywordAndMessage()
        {
            // The {field, keyword, message} contract from §7.2. A blank message
            // reaches a citizen; a blank field breaks the error summary's links.
            foreach (ValidationErrorModel error in Run(SimpleSpec, """{"monthlyIncome":"lots","unknownField":1}"""))
            {
                Assert.False(string.IsNullOrWhiteSpace(error.Field));
                Assert.False(string.IsNullOrWhiteSpace(error.Keyword));
                Assert.False(string.IsNullOrWhiteSpace(error.Message));
            }
        }

        private static IReadOnlyList<ValidationErrorModel> Run(string specJson, string answersJson)
        {
            using JsonDocument spec = JsonDocument.Parse(specJson);
            using JsonDocument answers = JsonDocument.Parse(answersJson);
            return FormSpecValidator.Validate(spec.RootElement, answers.RootElement);
        }
    }
}
