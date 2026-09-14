namespace Myss.Api.Tests.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="FormSpecValidator.ValidateSpecStructure"/> — the fast
    /// structural check that mirrors MyssContent's form-spec-rules.ts.
    /// </summary>
    public class FormSpecValidatorSpecStructureTests
    {
        [Fact]
        public void ValidSpec_ReturnsNoErrors()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate("""
                { "components": [ { "key": "firstName" }, { "key": "lastName" } ] }
                """);

            Assert.Empty(errors);
        }

        [Fact]
        public void NoComponentsArray_ReportsComponentsMissing()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate("""{ "display": "form" }""");

            Assert.Contains(FormSpecStructureKeywords.ComponentsMissing, Keywords(errors));
        }

        [Fact]
        public void EmptyComponents_ReportsComponentsEmpty()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate("""{ "components": [] }""");

            Assert.Contains(FormSpecStructureKeywords.ComponentsEmpty, Keywords(errors));
        }

        [Fact]
        public void ComponentWithoutKey_ReportsKeyMissing()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate("""
                { "components": [ { "type": "textfield" } ] }
                """);

            Assert.Contains(FormSpecStructureKeywords.ComponentKeyMissing, Keywords(errors));
        }

        [Fact]
        public void DuplicateKeys_AtTopLevel_ReportsDuplicate()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate("""
                { "components": [ { "key": "dupe" }, { "key": "dupe" } ] }
                """);

            Assert.Contains(FormSpecStructureKeywords.ComponentKeyDuplicate, Keywords(errors));
            Assert.Contains(errors, e => e.Field == "dupe");
        }

        [Fact]
        public void DuplicateKeys_NestedInsideAPanel_AreStillFound()
        {
            // The case a naive top-level loop misses, and exactly the shape a real
            // estimator spec has (fields inside panels/containers).
            IReadOnlyList<ValidationErrorModel> errors = Validate("""
                {
                  "components": [
                    { "key": "income" },
                    { "type": "panel", "key": "details", "components": [ { "key": "income" } ] }
                  ]
                }
                """);

            Assert.Contains(FormSpecStructureKeywords.ComponentKeyDuplicate, Keywords(errors));
        }

        [Fact]
        public void ConditionalPointingAtMissingField_ReportsUnknown()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate("""
                {
                  "components": [
                    { "key": "spouseName", "conditional": { "when": "hasSpouse", "show": true } }
                  ]
                }
                """);

            Assert.Contains(FormSpecStructureKeywords.ConditionalUnknownField, Keywords(errors));
            Assert.Contains(errors, e => e.Field == "hasSpouse");
        }

        [Fact]
        public void ConditionalPointingAtExistingField_IsAccepted()
        {
            IReadOnlyList<ValidationErrorModel> errors = Validate("""
                {
                  "components": [
                    { "key": "hasSpouse", "type": "checkbox" },
                    { "key": "spouseName", "conditional": { "when": "hasSpouse", "show": true } }
                  ]
                }
                """);

            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData("\"just a string\"")]
        [InlineData("123")]
        public void NonObjectSpec_ReportsSpecNotAnObject(string specJson)
        {
            // Match the authoritative lifecycle: a non-object spec is SPEC.NOT_AN_OBJECT,
            // not COMPONENTS.MISSING.
            IReadOnlyList<ValidationErrorModel> errors = Validate(specJson);

            Assert.Contains(FormSpecStructureKeywords.SpecNotAnObject, Keywords(errors));
            Assert.DoesNotContain(FormSpecStructureKeywords.ComponentsMissing, Keywords(errors));
        }

        private static IReadOnlyList<ValidationErrorModel> Validate(string specJson) =>
            FormSpecValidator.ValidateSpecStructure(JsonDocument.Parse(specJson).RootElement.Clone());

        private static IReadOnlyList<string> Keywords(IReadOnlyList<ValidationErrorModel> errors) =>
            errors.Select(e => e.Keyword).ToList();
    }
}
