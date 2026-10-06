namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestSupport;

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
        /// The custom BC Gov component types. <c>bcgovRadio</c> and
        /// <c>bcgovAddressAutocomplete</c> carry answers while
        /// <c>bcgovAccordion</c> does not, which is the distinction these tests
        /// exist to pin down.
        /// </summary>
        private const string CustomComponentSpec = """
        {
          "display": "form",
          "components": [
            { "type": "bcgovRadio", "key": "residesInBc", "input": true, "validate": { "required": true } },
            { "type": "bcgovAddressAutocomplete", "key": "streetAddress1", "input": true },
            { "type": "bcgovAccordion", "key": "statusHelp", "input": false },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private const string ConditionalSpec = """
        {
          "components": [
            { "type": "select", "key": "relationship", "input": true },
            { "type": "textfield", "key": "spouseName", "input": true,
              "validate": { "required": true },
              "conditional": { "show": true, "when": "relationship", "eq": "couple" } }
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
        public void Validate_SkipsTheRequiredCheck_WhenAConditionHidesTheField()
        {
            // The citizen never saw the field, so its answer cannot be required.
            Assert.Empty(Run(ConditionalSpec, """{"relationship":"single"}"""));
        }

        [Fact]
        public void Validate_RequiresAConditionalField_WhenItsConditionShowsIt()
        {
            // The gap recorded in AGENTS.md, closed: the simple `when`/`eq`/`show`
            // conditional is evaluated against the answers the way the form does.
            ValidationErrorModel error = Assert.Single(Run(ConditionalSpec, """{"relationship":"couple"}"""));

            Assert.Equal("spouseName", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Fact]
        public void Validate_HonoursAConditionThatHidesOnMatch()
        {
            const string spec = """
            {
              "components": [
                { "type": "radio", "key": "hasAccount", "input": true },
                { "type": "textfield", "key": "accountNumber", "input": true,
                  "validate": { "required": true },
                  "conditional": { "show": false, "when": "hasAccount", "eq": "no" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"hasAccount":"no"}"""));
            Assert.Single(Run(spec, """{"hasAccount":"yes"}"""));
        }

        [Fact]
        public void Validate_HidesEverythingInsideAHiddenContainer()
        {
            // A panel's conditional hides its children, so their required checks
            // follow the panel's visibility, not just their own.
            const string spec = """
            {
              "components": [
                { "type": "radio", "key": "mailingAddressDifferent", "input": true },
                { "type": "panel", "key": "mailingAddress",
                  "conditional": { "show": true, "when": "mailingAddressDifferent", "eq": "yes" },
                  "components": [
                    { "type": "textfield", "key": "mailingCity", "input": true, "validate": { "required": true } }
                  ] }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"mailingAddressDifferent":"no"}"""));
            Assert.Equal("mailingCity", Assert.Single(Run(spec, """{"mailingAddressDifferent":"yes"}""")).Field);
        }

        [Fact]
        public void Validate_ComparesAConditionAsText()
        {
            // Form.io compares `eq` with the answer as strings, so a boolean
            // checkbox and a numeric answer both match their textual `eq`.
            const string spec = """
            {
              "components": [
                { "type": "checkbox", "key": "agrees", "input": true },
                { "type": "textfield", "key": "signature", "input": true,
                  "validate": { "required": true },
                  "conditional": { "show": true, "when": "agrees", "eq": "true" } }
              ]
            }
            """;

            Assert.Single(Run(spec, """{"agrees":true}"""));
            Assert.Empty(Run(spec, """{"agrees":false}"""));
        }

        [Fact]
        public void Validate_EnforcesPatternAndLengthOnText()
        {
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "province", "input": true,
                  "validate": { "pattern": "^(BC|British Columbia)$" } },
                { "type": "textfield", "key": "lastName", "input": true,
                  "validate": { "minLength": 2, "maxLength": 5 } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"province":"BC","lastName":"Ada"}"""));

            IReadOnlyList<ValidationErrorModel> errors = Run(spec, """{"province":"AB","lastName":"A"}""");
            Assert.Equal(ValidationKeywords.FieldPattern, Assert.Single(errors, e => e.Field == "province").Keyword);
            Assert.Equal(ValidationKeywords.FieldMinLength, Assert.Single(errors, e => e.Field == "lastName").Keyword);

            Assert.Equal(
                ValidationKeywords.FieldMaxLength,
                Assert.Single(Run(spec, """{"province":"BC","lastName":"Lovelace"}""")).Keyword);
        }

        [Fact]
        public void Validate_EnforcesMinAndMax_OnNumbersAndNumericText()
        {
            // Form.io applies min/max to anything that parses as a number, so a
            // masked day-of-month text field with validate.min is covered too.
            const string spec = """
            {
              "components": [
                { "type": "number", "key": "income", "input": true, "validate": { "min": 0 } },
                { "type": "textfield", "key": "birthDay", "input": true, "validate": { "min": 1, "max": 31 } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"income":0,"birthDay":"31"}"""));

            IReadOnlyList<ValidationErrorModel> errors = Run(spec, """{"income":-1,"birthDay":"32"}""");
            Assert.Equal(ValidationKeywords.FieldMin, Assert.Single(errors, e => e.Field == "income").Keyword);
            Assert.Equal(ValidationKeywords.FieldMax, Assert.Single(errors, e => e.Field == "birthDay").Keyword);
        }

        [Fact]
        public void Validate_SkipsAPatternThatIsNotADotNetExpression()
        {
            // A designer's pattern the browser accepts but .NET cannot parse is
            // left to the browser rather than refusing every submission.
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "code", "input": true, "validate": { "pattern": "(?<=x)(" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"code":"anything"}"""));
        }

        [Fact]
        public void Validate_StillHoldsAHiddenFieldWithAValueToItsRules()
        {
            // The form clears a hidden field, so a value that arrives in one did
            // not come from a citizen. Only `required` follows visibility; the
            // value itself is never stored unchecked.
            const string spec = """
            {
              "components": [
                { "type": "radio", "key": "show", "input": true },
                { "type": "textfield", "key": "code", "input": true,
                  "validate": { "required": true, "pattern": "^[0-9]+$" },
                  "conditional": { "show": true, "when": "show", "eq": "yes" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"show":"no"}"""));
            Assert.Equal(ValidationKeywords.FieldPattern, Assert.Single(Run(spec, """{"show":"no","code":"abc"}""")).Keyword);
            Assert.Single(Run(spec, """{"show":"yes","code":"abc"}"""));
        }

        [Fact]
        public void Validate_TreatsAnUntickedRequiredCheckboxAsMissing()
        {
            // An unticked checkbox is `false` on the wire. Required means
            // "must be ticked", as Form.io's own rule reads it; otherwise an
            // acknowledgement could be posted as false and accepted.
            const string spec = """
            {
              "components": [
                { "type": "checkbox", "key": "acknowledged", "input": true, "validate": { "required": true } },
                { "type": "checkbox", "key": "optional", "input": true }
              ]
            }
            """;

            Assert.Equal("acknowledged", Assert.Single(Run(spec, """{"acknowledged":false,"optional":false}""")).Field);
            Assert.Empty(Run(spec, """{"acknowledged":true,"optional":false}"""));
        }

        [Fact]
        public void Validate_RequiresTheConfirmation_WhenThePartnerIsFilled()
        {
            // Leaving the confirmation empty must not satisfy the typo guard.
            const string spec = """
            {
              "components": [
                { "type": "email", "key": "email", "input": true },
                { "type": "email", "key": "confirm", "input": true, "properties": { "myssMatches": "email" } }
              ]
            }
            """;

            ValidationErrorModel error = Assert.Single(Run(spec, """{"email":"ada@example.com"}"""));
            Assert.Equal("confirm", error.Field);
            Assert.Equal(ValidationKeywords.EmailMismatch, error.Keyword);

            // Both empty is not a mismatch; the partner's own required rule, if any, speaks.
            Assert.Empty(Run(spec, """{}"""));
        }

        [Fact]
        public void Validate_RejectsATrailingNewline_LikeTheBrowserDoes()
        {
            // In .NET `$` also matches before a final newline; the browser's
            // JavaScript regex does not, and the two sides must agree.
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "sin", "input": true, "validate": { "pattern": "[0-9]{9}" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"sin":"123456789"}"""));
            Assert.Equal(ValidationKeywords.FieldPattern, Assert.Single(Run(spec, """{"sin":"123456789\n"}""")).Keyword);
        }

        [Fact]
        public void Validate_WordsAFailure_InTheOrderTheBrowserDoes()
        {
            // Form.io puts the catch-all customMessage ahead of the errors map
            // for its own rules; the wrappers put the errors entry first for the
            // named domain rules. The 422 must say what the field says.
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "code", "input": true,
                  "validate": { "pattern": "^[0-9]+$", "customMessage": "Custom", "patternMessage": "Pattern message" },
                  "errors": { "pattern": "From the map" } },
                { "type": "textfield", "key": "sin", "input": true,
                  "validate": { "customMessage": "Custom" },
                  "errors": { "sin": "From the map" },
                  "properties": { "myssValidator": "sin" } },
                { "type": "textfield", "key": "other", "input": true,
                  "validate": { "pattern": "^[0-9]+$", "patternMessage": "Pattern message" },
                  "errors": { "pattern": "From the map" } }
              ]
            }
            """;

            IReadOnlyList<ValidationErrorModel> errors = Run(spec, """{"code":"x","sin":"050082830","other":"x"}""");

            Assert.Equal("Custom", Assert.Single(errors, e => e.Field == "code").Message);
            Assert.Equal("From the map", Assert.Single(errors, e => e.Field == "sin").Message);
            Assert.Equal("Pattern message", Assert.Single(errors, e => e.Field == "other").Message);
        }

        [Fact]
        public void Validate_WordsAFailureFromTheFormsErrorsMap_AndFlagsItAsAuthored()
        {
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "postalCode", "input": true,
                  "validate": { "required": true, "pattern": "^[A-Z][0-9][A-Z] ?[0-9][A-Z][0-9]$" },
                  "errors": { "required": "A postal code is required", "pattern": "Invalid postal code format" } }
              ]
            }
            """;

            ValidationErrorModel missing = Assert.Single(Run(spec, """{}"""));
            Assert.Equal("A postal code is required", missing.Message);
            Assert.True(missing.HasAuthoredMessage);

            ValidationErrorModel malformed = Assert.Single(Run(spec, """{"postalCode":"nope"}"""));
            Assert.Equal("Invalid postal code format", malformed.Message);
            Assert.True(malformed.HasAuthoredMessage);
        }

        [Fact]
        public void Validate_FallsBackToTheCustomMessage_ThenTheDefault()
        {
            const string spec = """
            {
              "components": [
                { "type": "checkbox", "key": "acknowledged", "input": true,
                  "validate": { "required": true, "customMessage": "Acknowledgement is required" } },
                { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } }
              ]
            }
            """;

            IReadOnlyList<ValidationErrorModel> errors = Run(spec, """{}""");

            ValidationErrorModel custom = Assert.Single(errors, e => e.Field == "acknowledged");
            Assert.Equal("Acknowledgement is required", custom.Message);
            Assert.True(custom.HasAuthoredMessage);

            ValidationErrorModel plain = Assert.Single(errors, e => e.Field == "firstName");
            Assert.Equal("This answer is required.", plain.Message);
            Assert.False(plain.HasAuthoredMessage);
        }

        [Fact]
        public void Validate_AppliesPhonePostalCodeAndDateRules()
        {
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "phone", "input": true, "properties": { "myssValidator": "phone" } },
                { "type": "textfield", "key": "postal", "input": true, "properties": { "myssValidator": "postalCode" } },
                { "type": "textfield", "key": "started", "input": true, "properties": { "myssValidator": "date" } },
                { "type": "phoneNumber", "key": "mobile", "input": true }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"phone":"(250) 555-0199","postal":"V8V 1X4","started":"2024-02-29","mobile":"2505550199"}"""));

            IReadOnlyList<ValidationErrorModel> errors = Run(spec, """{"phone":"555","postal":"12345","started":"2023-02-29","mobile":"1"}""");
            Assert.Equal(ValidationKeywords.PhoneInvalidFormat, Assert.Single(errors, e => e.Field == "phone").Keyword);
            Assert.Equal(ValidationKeywords.PostalCodeInvalidFormat, Assert.Single(errors, e => e.Field == "postal").Keyword);
            Assert.Equal(ValidationKeywords.DateInvalid, Assert.Single(errors, e => e.Field == "started").Keyword);
            Assert.Equal(ValidationKeywords.PhoneInvalidFormat, Assert.Single(errors, e => e.Field == "mobile").Keyword);
        }

        private const string DatePartsSpec = """
        {
          "components": [
            { "type": "textfield", "key": "birthDay", "input": true,
              "properties": { "myssValidator": "dateParts", "myssDateParts": { "month": "birthMonth", "year": "birthYear" } } },
            { "type": "select", "key": "birthMonth", "input": true },
            { "type": "textfield", "key": "birthYear", "input": true }
          ]
        }
        """;

        public static IEnumerable<object[]> ValidDateParts =>
            ValidationVectors.AsTheoryData(ValidationVectors.DateParts("valid"));

        public static IEnumerable<object[]> InvalidDateParts =>
            ValidationVectors.AsTheoryData(ValidationVectors.DateParts("invalid"));

        public static IEnumerable<object[]> IncompleteDateParts =>
            ValidationVectors.AsTheoryData(ValidationVectors.DateParts("incomplete"));

        [Theory]
        [MemberData(nameof(ValidDateParts))]
        public void DateParts_AcceptsEveryValidVector(string day, string month, string year, string _)
        {
            Assert.Empty(Run(DatePartsSpec, DatePartsAnswers(day, month, year)));
        }

        [Theory]
        [MemberData(nameof(InvalidDateParts))]
        public void DateParts_RefusesEveryInvalidVector_OnTheDayField(string day, string month, string year, string expectedKeyword)
        {
            ValidationErrorModel error = Assert.Single(Run(DatePartsSpec, DatePartsAnswers(day, month, year)));
            Assert.Equal("birthDay", error.Field);
            Assert.Equal(expectedKeyword, error.Keyword);
        }

        [Theory]
        [MemberData(nameof(IncompleteDateParts))]
        public void DateParts_LeavesAnIncompleteGroupToTheOtherRules(string day, string month, string year, string _)
        {
            Assert.Empty(Run(DatePartsSpec, DatePartsAnswers(day, month, year)));
        }

        private static string DatePartsAnswers(string day, string month, string year) =>
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["birthDay"] = day,
                ["birthMonth"] = month,
                ["birthYear"] = year,
            });

        [Fact]
        public void Validate_ReadsABlankShow_AsHideOnMatch_LikeFormIo()
        {
            // Form.io reads `String(show) === "true"`, and its builder leaves
            // `show` blank unless the designer picks "Show". Blank must hide the
            // field when the answer matches, or the server requires a field the
            // citizen cannot see.
            const string spec = """
            {
              "components": [
                { "type": "radio", "key": "hasAccount", "input": true },
                { "type": "textfield", "key": "accountNumber", "input": true,
                  "validate": { "required": true },
                  "conditional": { "when": "hasAccount", "eq": "no" } },
                { "type": "textfield", "key": "other", "input": true,
                  "validate": { "required": true },
                  "conditional": { "when": "hasAccount", "eq": "no", "show": "false" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"hasAccount":"no"}"""));
            Assert.Equal(2, Run(spec, """{"hasAccount":"yes"}""").Count);
        }

        [Fact]
        public void Validate_LeavesAHiddenFieldAlone_WhenTheFormKeepsItsValue()
        {
            // With "Clear Value When Hidden" off, Form.io keeps and submits the
            // hidden value and does not validate it; refusing it would strand the
            // citizen on a field they cannot see.
            const string spec = """
            {
              "components": [
                { "type": "radio", "key": "show", "input": true },
                { "type": "textfield", "key": "code", "input": true, "clearOnHide": false,
                  "validate": { "required": true, "pattern": "^[0-9]+$" },
                  "conditional": { "show": true, "when": "show", "eq": "yes" } },
                { "type": "textfield", "key": "checked", "input": true, "clearOnHide": false, "validateWhenHidden": true,
                  "validate": { "pattern": "^[0-9]+$" },
                  "conditional": { "show": true, "when": "show", "eq": "yes" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"show":"no","code":"abc"}"""));
            Assert.Equal("checked", Assert.Single(Run(spec, """{"show":"no","checked":"abc"}""")).Field);
            Assert.Equal("code", Assert.Single(Run(spec, """{"show":"yes","code":"abc"}""")).Field);
        }

        [Fact]
        public void Validate_AcceptsAChoiceFormIoSendsAsANumberOrBoolean()
        {
            // Form.io posts the option "12" as the number 12 and "true" as true.
            const string spec = """
            {
              "components": [
                { "type": "select", "key": "month", "input": true, "validate": { "required": true } },
                { "type": "radio", "key": "agrees", "input": true, "validate": { "required": true } },
                { "type": "textfield", "key": "note", "input": true,
                  "validate": { "required": true },
                  "conditional": { "show": true, "when": "month", "eq": "12" } }
              ]
            }
            """;

            // The numeric choice still drives the conditional as text.
            ValidationErrorModel error = Assert.Single(Run(spec, """{"month":12,"agrees":true}"""));
            Assert.Equal("note", error.Field);
            Assert.Empty(Run(spec, """{"month":12,"agrees":false,"note":"x"}"""));
        }

        [Fact]
        public void Validate_ReportsOneFailurePerField()
        {
            // A day of "0" is too small and not a date; the form shows one
            // reason, and so does the API.
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "birthDay", "input": true,
                  "validate": { "min": 1, "max": 31 },
                  "properties": { "myssValidator": "dateParts", "myssDateParts": { "month": "birthMonth", "year": "birthYear" } } },
                { "type": "select", "key": "birthMonth", "input": true },
                { "type": "textfield", "key": "birthYear", "input": true }
              ]
            }
            """;

            ValidationErrorModel error = Assert.Single(Run(spec, """{"birthDay":"0","birthMonth":"01","birthYear":"2000"}"""));
            Assert.Equal(ValidationKeywords.FieldMin, error.Keyword);
        }

        [Fact]
        public void Validate_ReadsADesignerPattern_TheWayJavaScriptDoes()
        {
            // `\d` is ASCII in JavaScript; in .NET it would also match other
            // scripts' digits and accept what the browser refuses.
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "code", "input": true, "validate": { "pattern": "\\d{3}" } }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"code":"123"}"""));
            Assert.Single(Run(spec, """{"code":"١٢٣"}"""));
        }

        [Fact]
        public void Validate_AppliesTheDatePartsRule_ToACompleteGroupOnly()
        {
            const string spec = """
            {
              "components": [
                { "type": "textfield", "key": "birthDay", "input": true,
                  "properties": { "myssValidator": "dateParts", "myssDateParts": { "month": "birthMonth", "year": "birthYear" } } },
                { "type": "select", "key": "birthMonth", "input": true },
                { "type": "textfield", "key": "birthYear", "input": true }
              ]
            }
            """;

            Assert.Empty(Run(spec, """{"birthDay":"29","birthMonth":"02","birthYear":"2000"}"""));

            ValidationErrorModel error = Assert.Single(Run(spec, """{"birthDay":"31","birthMonth":"02","birthYear":"2001"}"""));
            Assert.Equal("birthDay", error.Field);
            Assert.Equal(ValidationKeywords.DateInvalid, error.Keyword);

            // A missing part is the required rule's business.
            Assert.Empty(Run(spec, """{"birthDay":"31","birthYear":"2001"}"""));
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
        public void Validate_AcceptsABcgovRadioAnswerInEveryShapeFormIoSends_ButNotAnObject()
        {
            // Form.io posts the option "true" as a boolean and "12" as a number,
            // and every reader downstream takes a choice as text. An object is
            // a shape no radio produces.
            Assert.Empty(Run(CustomComponentSpec, """{"residesInBc":true}"""));
            Assert.Empty(Run(CustomComponentSpec, """{"residesInBc":12}"""));

            ValidationErrorModel error =
                Assert.Single(Run(CustomComponentSpec, """{"residesInBc":{"yes":true}}"""));

            Assert.Equal("residesInBc", error.Field);
            Assert.Equal(ValidationKeywords.FieldWrongType, error.Keyword);
        }

        [Fact]
        public void Validate_AcceptsAStringBcgovAddressAutocompleteAnswer()
        {
            Assert.Empty(Run(
                CustomComponentSpec,
                """{"residesInBc":"true","streetAddress1":"501 Belleville St"}"""));
        }

        [Fact]
        public void Validate_RejectsANonStringBcgovAddressAutocompleteAnswer()
        {
            ValidationErrorModel error = Assert.Single(Run(
                CustomComponentSpec,
                """{"residesInBc":"true","streetAddress1":{"line1":"501 Belleville St"}}"""));

            Assert.Equal("streetAddress1", error.Field);
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
