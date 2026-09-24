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

        /// <summary>
        /// The two custom BC Gov component types. <c>bcgovRadio</c> carries an
        /// answer and <c>bcgovAccordion</c> does not, which is the distinction
        /// these tests exist to pin down.
        /// </summary>
        private const string CustomComponentSpec = """
        {
          "display": "form",
          "components": [
            { "type": "bcgovRadio", "key": "residesInBc", "input": true, "validate": { "required": true } },
            { "type": "bcgovAccordion", "key": "statusHelp", "input": false },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private const string NestedRadioSpec = """
        {
          "display": "form",
          "components": [
            { "type": "bcgovNestedRadio", "key": "serviceRequestType", "input": true, "validate": { "required": true } },
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
            // Known gap, deliberate: whether a conditional field is required
            // depends on other answers, and Form.io conditional logic is not
            // evaluated server-side. Recorded in AGENTS.md; it must be closed
            // before a form relies on conditionally-required fields.
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
            // without a dedicated client-side component.
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
            // The type route: how a dedicated SIN component would declare
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
            // Every error must carry all three fields. A blank message reaches
            // a citizen; a blank field breaks the error summary's links.
            foreach (ValidationErrorModel error in Run(SimpleSpec, """{"monthlyIncome":"lots","unknownField":1}"""))
            {
                Assert.False(string.IsNullOrWhiteSpace(error.Field));
                Assert.False(string.IsNullOrWhiteSpace(error.Keyword));
                Assert.False(string.IsNullOrWhiteSpace(error.Message));
            }
        }

        [Fact]
        public void Validate_AcceptsAStringBcgovRadioAnswer()
        {
            Assert.Empty(Run(CustomComponentSpec, """{"residesInBc":"true"}"""));
        }

        [Fact]
        public void Validate_AcceptsAStringNestedRadioAnswer()
        {
          Assert.Empty(Run(NestedRadioSpec, """{"serviceRequestType":"addressUpdate"}"""));
        }

        [Fact]
        public void Validate_RejectsANonStringNestedRadioAnswer()
        {
          ValidationErrorModel error =
            Assert.Single(Run(NestedRadioSpec, """{"serviceRequestType":true}"""));

          Assert.Equal("serviceRequestType", error.Field);
          Assert.Equal(ValidationKeywords.FieldWrongType, error.Keyword);
        }

        [Fact]
        public void Validate_RejectsANonStringBcgovRadioAnswer()
        {
            // The radio pins dataType "string", so the literal "true"/"false" is
            // what reaches the server. A real boolean means the client sent a
            // shape the conditionals downstream will not match.
            ValidationErrorModel error =
                Assert.Single(Run(CustomComponentSpec, """{"residesInBc":true}"""));

            Assert.Equal("residesInBc", error.Field);
            Assert.Equal(ValidationKeywords.FieldWrongType, error.Keyword);
        }

        [Fact]
        public void Validate_NeverTreatsABcgovAccordionAsAnAnsweredField()
        {
            // Display only, so it must never block a submission. The second case
            // is the discriminating one: marking a non-data component required is
            // a spec the admin panel permits, and treating it as a field would
            // then demand an answer that cannot be supplied.
            Assert.Empty(Run(CustomComponentSpec, """{"residesInBc":"true","statusHelp":"anything"}"""));

            const string accordionMarkedRequired = """
            {
              "display": "form",
              "components": [
                { "type": "bcgovRadio", "key": "residesInBc", "input": true, "validate": { "required": true } },
                { "type": "bcgovAccordion", "key": "statusHelp", "input": false, "validate": { "required": true } },
                { "type": "button", "key": "submit", "action": "submit", "input": true }
              ]
            }
            """;

            Assert.Empty(Run(accordionMarkedRequired, """{"residesInBc":"true"}"""));
        }

        private static IReadOnlyList<ValidationErrorModel> Run(string specJson, string answersJson)
        {
            using JsonDocument spec = JsonDocument.Parse(specJson);
            using JsonDocument answers = JsonDocument.Parse(answersJson);
            return FormSpecValidator.Validate(spec.RootElement, answers.RootElement);
        }
    }
}
