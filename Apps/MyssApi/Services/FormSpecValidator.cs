namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Myss.Api.Domain;
    using Myss.Api.Models;

    /// <summary>
    /// Validates a submitted answers object against the Form.io spec version it
    /// claims to have been rendered with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Client-side validation is a UX convenience and never a security
    /// control: the back-end re-validates every value regardless of what the
    /// form said. This class is that re-validation. It is pure — spec in, answers in, failures out — so it can
    /// be exercised without a database, an HTTP request or a browser.
    /// </para>
    /// <para><b>What it enforces.</b> The rules the spec declares in Form.io's
    /// own vocabulary (<c>validate.required</c>, <c>pattern</c>, <c>minLength</c>,
    /// <c>maxLength</c>, <c>min</c>, <c>max</c>), the JSON type each component
    /// implies, and the domain rules a field opts into. Simple conditionals
    /// (<c>conditional.when</c> / <c>eq</c> / <c>show</c>, on the component or
    /// on a container above it) are evaluated against the answers, so a field
    /// is required only when the citizen could see it. Advanced (JSON logic)
    /// conditionals are not evaluated; a field behind one counts as visible.</para>
    /// <para><b>How a field opts in to a domain rule.</b> Form.io has no notion
    /// of a Canadian SIN, so the spec must say which rule applies. Two ways,
    /// checked in order:</para>
    /// <list type="number">
    /// <item><description>An explicit marker in the component's
    /// <c>properties</c> map: <c>{ "myssValidator": "sin" }</c> (also
    /// <c>email</c>, <c>phone</c>, <c>postalCode</c>, <c>date</c>, and
    /// <c>dateParts</c> with <c>myssDateParts: { month, year }</c> naming the
    /// sibling fields). Form.io's <c>properties</c> is free-form key-value, so
    /// this is authored as ordinary content on an ordinary field — no code, no
    /// deployment. A confirmation field adds <c>{ "myssMatches": "contactEmail" }</c>.</description></item>
    /// <item><description>The component <c>type</c> itself, for custom
    /// components such as <c>sin</c>, and for Form.io's own <c>email</c>
    /// and <c>phoneNumber</c> types.</description></item>
    /// </list>
    /// <para><b>Wording.</b> A failure's message comes from the form first: the
    /// component's <c>errors</c> map for the rule, then its
    /// <c>validate.customMessage</c>, which are then flagged as authored so the
    /// catalogue never replaces them. Otherwise the compiled default, which
    /// <see cref="ErrorMessageResolver"/> swaps for the catalogue's wording.</para>
    /// </remarks>
    public static class FormSpecValidator
    {
        /// <summary>
        /// Component types that carry no citizen answer. A Form.io submit button
        /// has <c>input: true</c> like any field does, so "is this a data field"
        /// cannot be decided from that flag alone — without this list every
        /// form would report its own submit button as an unknown key.
        /// </summary>
        private static readonly HashSet<string> NonDataTypes =
        [
            "button", "content", "htmlelement", "panel", "columns",
            "fieldset", "well", "table", "tabs",

            // Custom BC Gov display component, rendered client-side by a
            // registered Form.io component. Carries no citizen answer, so treat it
            // like the other non-data content components.
            "bcgovAccordion",
        ];

        /// <summary>
        /// A spec pattern is a regular expression the designer typed; a slow one
        /// must not hold a request, and one that is not a .NET expression at all
        /// is skipped rather than refused.
        /// </summary>
        private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Validates answers against a spec.
        /// </summary>
        /// <param name="spec">The Form.io spec body.</param>
        /// <param name="answers">The submitted answers, keyed by component key.</param>
        /// <returns>Every failure found. Empty when the submission is acceptable.</returns>
        public static IReadOnlyList<ValidationErrorModel> Validate(JsonElement spec, JsonElement answers)
        {
            List<ValidationErrorModel> errors = [];

            if (answers.ValueKind != JsonValueKind.Object)
            {
                errors.Add(Error("answers", ValidationKeywords.FieldWrongType, "Answers must be an object."));
                return errors;
            }

            Dictionary<string, ComponentInfo> components = [];
            HashSet<string> tolerated = [];
            if (spec.ValueKind == JsonValueKind.Object)
            {
                Collect(spec, components, tolerated, []);
            }

            // Unknown keys first: an answer the spec has no field for is either a
            // client bug or someone probing the endpoint. Either way it must not
            // be persisted silently.
            foreach (JsonProperty answer in answers.EnumerateObject())
            {
                if (!components.ContainsKey(answer.Name) && !tolerated.Contains(answer.Name))
                {
                    errors.Add(Error(
                        answer.Name,
                        ValidationKeywords.FieldUnknown,
                        $"\"{answer.Name}\" is not a field on this form."));
                }
            }

            foreach ((string key, ComponentInfo component) in components)
            {
                bool visible = component.IsVisible(answers);
                bool present = answers.TryGetProperty(key, out JsonElement value) && !IsEmpty(component.Type, value);

                if (!present)
                {
                    if (component.Required && visible)
                    {
                        errors.Add(RuleError(component, "required", ValidationKeywords.FieldRequired, "This answer is required."));
                    }

                    // An empty confirmation of a filled partner is a mismatch, not
                    // a blank: the typo guard must not be satisfiable by leaving
                    // the second field out.
                    if (component.MatchesKey is not null && visible && AnswerText(answers, component.MatchesKey).Trim().Length > 0)
                    {
                        errors.Add(RuleError(component, "matches", ValidationKeywords.EmailMismatch, "The two email addresses do not match."));
                    }

                    continue;
                }

                if (!TypeMatches(component.Type, value))
                {
                    errors.Add(Error(
                        key,
                        ValidationKeywords.FieldWrongType,
                        $"\"{key}\" was not submitted in the expected format."));
                    continue;
                }

                // A hidden field's value is normally cleared by the form, so one
                // that arrives anyway did not come from a citizen and is held to
                // the field's rules rather than stored unchecked. The exception
                // is a field the designer set to keep its value when hidden: the
                // form keeps and submits that value without validating it, and
                // the citizen cannot see the field to correct it.
                if (!visible && component.KeepsValueWhenHidden && !component.ValidateWhenHidden)
                {
                    continue;
                }

                ApplyFormatRules(component, value, errors);
                ApplyDomainRules(component, value, answers, errors);
            }

            return OnePerField(errors);
        }

        /// <summary>
        /// One failure per field, the first found, which is how the form shows
        /// them: a day of "0" is "too small", not also "not a date".
        /// </summary>
        /// <param name="errors">Failures in the order they were found.</param>
        /// <returns>The first failure of each field, in that order.</returns>
        public static IReadOnlyList<ValidationErrorModel> OnePerField(IEnumerable<ValidationErrorModel> errors)
        {
            List<ValidationErrorModel> kept = [];
            HashSet<string> seen = [];
            foreach (ValidationErrorModel error in errors)
            {
                if (seen.Add(error.Field))
                {
                    kept.Add(error);
                }
            }

            return kept;
        }

        /// <summary>
        /// Validates the STRUCTURE of a form spec (not answers): the fast, cheap
        /// checks a designer can trip over just by editing. Mirrors the structural
        /// subset of MyssContent's <c>form-spec-rules.ts</c> — a non-empty
        /// <c>components</c> array, every component keyed, keys unique across the
        /// whole form, and every <c>conditional.when</c> pointing at a real field.
        /// Version sequence and immutability are left to Strapi's lifecycle, the
        /// only place that can see the other rows; this is a fast 422, not the gate.
        /// </summary>
        /// <param name="spec">The Form.io spec body.</param>
        /// <returns>Every structural failure found. Empty when the spec is well-formed.</returns>
        public static IReadOnlyList<ValidationErrorModel> ValidateSpecStructure(JsonElement spec)
        {
            if (spec.ValueKind != JsonValueKind.Object)
            {
                return [Error("spec", FormSpecStructureKeywords.SpecNotAnObject, "The form spec must be a JSON object with a `components` array.")];
            }

            if (!spec.TryGetProperty("components", out JsonElement componentsArray)
                || componentsArray.ValueKind != JsonValueKind.Array)
            {
                return [Error("components", FormSpecStructureKeywords.ComponentsMissing, "The form spec must have a `components` array.")];
            }

            List<JsonElement> components = [];
            CollectAll(spec, components);

            if (components.Count == 0)
            {
                return [Error("components", FormSpecStructureKeywords.ComponentsEmpty, "The form spec has no components.")];
            }

            List<ValidationErrorModel> errors = [];
            HashSet<string> seen = [];
            SortedSet<string> duplicates = [];
            bool missingKeyReported = false;

            foreach (JsonElement component in components)
            {
                string? key = component.TryGetProperty("key", out JsonElement k) && k.ValueKind == JsonValueKind.String
                    ? k.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(key))
                {
                    // Report the missing-key fault once: the message is identical for
                    // every keyless component, so repeating it only clutters the summary.
                    if (!missingKeyReported)
                    {
                        errors.Add(Error("components", FormSpecStructureKeywords.ComponentKeyMissing, "Every component needs a non-empty `key`."));
                        missingKeyReported = true;
                    }

                    continue;
                }

                if (!seen.Add(key))
                {
                    duplicates.Add(key);
                }
            }

            foreach (string key in duplicates)
            {
                errors.Add(Error(key, FormSpecStructureKeywords.ComponentKeyDuplicate, $"Duplicate component key \"{key}\". Keys must be unique across the whole form."));
            }

            SortedSet<string> unknownTargets = [];
            foreach (JsonElement component in components)
            {
                if (component.TryGetProperty("conditional", out JsonElement cond)
                    && cond.ValueKind == JsonValueKind.Object
                    && cond.TryGetProperty("when", out JsonElement when)
                    && when.ValueKind == JsonValueKind.String
                    && when.GetString() is { Length: > 0 } target
                    && !seen.Contains(target))
                {
                    unknownTargets.Add(target);
                }
            }

            foreach (string target in unknownTargets)
            {
                errors.Add(Error(target, FormSpecStructureKeywords.ConditionalUnknownField, $"A conditional refers to \"{target}\", which is not a field in this form."));
            }

            return errors;
        }

        /// <summary>
        /// Walks every component node (panels included), so key-uniqueness and
        /// conditional checks see the whole tree. Unlike <see cref="Collect"/> this
        /// keeps container components too, because a duplicate key on a panel is
        /// still a duplicate.
        /// </summary>
        private static void CollectAll(JsonElement node, List<JsonElement> into)
        {
            if (node.TryGetProperty("components", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in children.EnumerateArray())
                {
                    if (child.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    into.Add(child);
                    CollectAll(child, into);
                }
            }

            if (node.TryGetProperty("columns", out JsonElement columns) && columns.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement column in columns.EnumerateArray())
                {
                    if (column.ValueKind == JsonValueKind.Object)
                    {
                        CollectAll(column, into);
                    }
                }
            }

            if (node.TryGetProperty("rows", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement row in rows.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement cell in row.EnumerateArray())
                    {
                        if (cell.ValueKind == JsonValueKind.Object)
                        {
                            CollectAll(cell, into);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The rules the spec declares in Form.io's own vocabulary, applied the
        /// way Form.io applies them: pattern and length to text, min and max to
        /// anything that reads as a number (a masked day-of-month text field has
        /// <c>validate.min</c> too).
        /// </summary>
        private static void ApplyFormatRules(ComponentInfo component, JsonElement value, List<ValidationErrorModel> errors)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                string text = value.GetString() ?? string.Empty;

                if (component.Pattern is not null && !MatchesPattern(text, component.Pattern))
                {
                    errors.Add(RuleError(component, "pattern", ValidationKeywords.FieldPattern, "This answer is not in the expected format."));
                }

                if (component.MinLength is int minLength && text.Length < minLength)
                {
                    errors.Add(RuleError(component, "minLength", ValidationKeywords.FieldMinLength, "This answer is too short."));
                }

                if (component.MaxLength is int maxLength && text.Length > maxLength)
                {
                    errors.Add(RuleError(component, "maxLength", ValidationKeywords.FieldMaxLength, "This answer is too long."));
                }
            }

            if ((component.Min is not null || component.Max is not null) && TryReadNumber(value, out decimal number))
            {
                if (component.Min is decimal min && number < min)
                {
                    errors.Add(RuleError(component, "min", ValidationKeywords.FieldMin, "This number is too small."));
                }

                if (component.Max is decimal max && number > max)
                {
                    errors.Add(RuleError(component, "max", ValidationKeywords.FieldMax, "This number is too large."));
                }
            }
        }

        private static void ApplyDomainRules(
            ComponentInfo component,
            JsonElement value,
            JsonElement answers,
            List<ValidationErrorModel> errors)
        {
            string raw = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

            switch (component.Validator)
            {
                case "sin":
                    AddDomainFailure(component, "sin", Sin.TryCreate(raw), errors);
                    break;

                case "email":
                    AddDomainFailure(component, "email", EmailAddress.TryCreate(raw), errors);
                    break;

                case "phone":
                    AddDomainFailure(component, "phone", PhoneNumber.TryCreate(raw), errors);
                    break;

                case "postalCode":
                    AddDomainFailure(component, "postalCode", PostalCode.TryCreate(raw), errors);
                    break;

                case "date":
                    if (!IsoDate.TryParse(raw, out _))
                    {
                        errors.Add(RuleError(component, "date", ValidationKeywords.DateInvalid, "Enter a valid date."));
                    }

                    break;

                case "dateParts":
                    // The day of a day/month/year group. A part that is missing or
                    // not a number is the required or pattern rule's business; a
                    // complete numeric group must make a real date.
                    if (TryReadDatePart(raw, out int day)
                        && TryReadDatePart(AnswerText(answers, component.DatePartsMonthKey), out int month)
                        && TryReadDatePart(AnswerText(answers, component.DatePartsYearKey), out int year)
                        && !IsoDate.TryCreate(year, month, day, out _))
                    {
                        errors.Add(RuleError(component, "dateParts", ValidationKeywords.DateInvalid, "Enter a valid date."));
                    }

                    break;

                default:
                    break;
            }

            // A confirmation field names the field it confirms. The failure is
            // reported against the confirmation, which is where the citizen's
            // focus should land.
            if (component.MatchesKey is not null
                && !EmailAddress.ConfirmationMatches(AnswerText(answers, component.MatchesKey), raw))
            {
                errors.Add(RuleError(component, "matches", ValidationKeywords.EmailMismatch, "The two email addresses do not match."));
            }
        }

        private static void AddDomainFailure<T>(
            ComponentInfo component,
            string rule,
            DomainValidationResult<T> result,
            List<ValidationErrorModel> errors)
            where T : class
        {
            if (!result.IsValid)
            {
                errors.Add(RuleError(component, rule, result.Keyword!, result.Message!));
            }
        }

        /// <summary>
        /// Walks the component tree. Form.io nests fields inside panels, columns,
        /// fieldsets, table cells and wizard pages, so a top-level scan would miss
        /// most of a real form.
        /// </summary>
        /// <param name="node">The node whose children to collect.</param>
        /// <param name="into">Accumulator, keyed by component key.</param>
        /// <param name="tolerated">Keys of non-data components: not validated, not rejected.</param>
        /// <param name="conditions">The conditionals on every container above this node.</param>
        private static void Collect(
            JsonElement node,
            Dictionary<string, ComponentInfo> into,
            HashSet<string> tolerated,
            IReadOnlyList<SimpleCondition> conditions)
        {
            if (node.TryGetProperty("components", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in children.EnumerateArray())
                {
                    Visit(child, into, tolerated, conditions);
                }
            }

            if (node.TryGetProperty("columns", out JsonElement columns) && columns.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement column in columns.EnumerateArray())
                {
                    Collect(column, into, tolerated, conditions);
                }
            }

            if (node.TryGetProperty("rows", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement row in rows.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement cell in row.EnumerateArray())
                    {
                        Collect(cell, into, tolerated, conditions);
                    }
                }
            }
        }

        private static void Visit(
            JsonElement component,
            Dictionary<string, ComponentInfo> into,
            HashSet<string> tolerated,
            IReadOnlyList<SimpleCondition> inherited)
        {
            if (component.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            string type = component.TryGetProperty("type", out JsonElement t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? string.Empty
                : string.Empty;

            // A container's conditional hides everything inside it, so the chain
            // of conditions a field answers to is its own plus its ancestors'.
            IReadOnlyList<SimpleCondition> conditions = inherited;
            if (SimpleCondition.TryRead(component, out SimpleCondition own))
            {
                conditions = [.. inherited, own];
            }

            if (component.TryGetProperty("key", out JsonElement k)
                && k.ValueKind == JsonValueKind.String
                && k.GetString() is { Length: > 0 } key)
            {
                if (NonDataTypes.Contains(type))
                {
                    // Form.io posts the submit button's own value back in the
                    // submission data (`"submit": true`). It is not a field, so
                    // it is neither validated nor rejected — without this, every
                    // genuine Form.io submission fails as an unknown key.
                    tolerated.Add(key);
                }
                else
                {
                    into.TryAdd(key, Describe(key, type, component, conditions));
                }
            }

            // Recurse regardless: a panel is not a field but holds them.
            Collect(component, into, tolerated, conditions);
        }

        private static ComponentInfo Describe(
            string key,
            string type,
            JsonElement component,
            IReadOnlyList<SimpleCondition> conditions)
        {
            JsonElement validate = component.TryGetProperty("validate", out JsonElement v) && v.ValueKind == JsonValueKind.Object
                ? v
                : default;

            bool required = validate.ValueKind == JsonValueKind.Object
                && validate.TryGetProperty("required", out JsonElement req)
                && req.ValueKind == JsonValueKind.True;

            string? validator = null;
            string? matches = null;
            string? monthKey = null;
            string? yearKey = null;

            if (component.TryGetProperty("properties", out JsonElement props) && props.ValueKind == JsonValueKind.Object)
            {
                validator = ReadString(props, "myssValidator");
                matches = ReadString(props, "myssMatches");

                if (props.TryGetProperty("myssDateParts", out JsonElement parts) && parts.ValueKind == JsonValueKind.Object)
                {
                    monthKey = ReadString(parts, "month");
                    yearKey = ReadString(parts, "year");
                }
            }

            // Fall back to the component type, which is how custom components
            // and Form.io's own email and phone types declare themselves.
            validator ??= type switch
            {
                "sin" => "sin",
                "email" => "email",
                "phoneNumber" => "phone",
                _ => null,
            };

            Dictionary<string, string> errors = new(StringComparer.Ordinal);
            if (component.TryGetProperty("errors", out JsonElement errorMap) && errorMap.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty entry in errorMap.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(entry.Value.GetString()))
                    {
                        errors[entry.Name] = entry.Value.GetString()!;
                    }
                }
            }

            return new ComponentInfo(
                key,
                type,
                required,
                validator,
                matches,
                monthKey,
                yearKey,
                ReadString(validate, "pattern"),
                ReadInt(validate, "minLength"),
                ReadInt(validate, "maxLength"),
                ReadDecimal(validate, "min"),
                ReadDecimal(validate, "max"),
                errors,
                ReadString(validate, "customMessage"),
                ReadString(validate, "patternMessage"),
                KeepsValueWhenHidden: component.TryGetProperty("clearOnHide", out JsonElement clearOnHide)
                    && clearOnHide.ValueKind == JsonValueKind.False,
                ValidateWhenHidden: component.TryGetProperty("validateWhenHidden", out JsonElement validateWhenHidden)
                    && validateWhenHidden.ValueKind == JsonValueKind.True,
                conditions);
        }

        private static string? ReadString(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
                ? text
                : null;

        /// <summary>A length limit, as a number or as the string the builder sometimes stores.</summary>
        private static int? ReadInt(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out int number) => number,
                JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
                _ => null,
            };
        }

        /// <summary>A numeric limit, as a number or as the string the builder sometimes stores.</summary>
        private static decimal? ReadDecimal(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            return TryReadNumber(value, out decimal number) ? number : null;
        }

        private static bool TryReadNumber(JsonElement value, out decimal number)
        {
            number = 0;
            return value.ValueKind switch
            {
                JsonValueKind.Number => value.TryGetDecimal(out number),
                JsonValueKind.String => decimal.TryParse(
                    value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number),
                _ => false,
            };
        }

        /// <summary>
        /// A date part as typed: up to four ASCII digits. The browser's rule
        /// reads the same, so "100" is a bad day on both sides rather than a
        /// non-number the browser ignores.
        /// </summary>
        private static bool TryReadDatePart(string? text, out int part)
        {
            part = 0;
            string trimmed = (text ?? string.Empty).Trim();
            return trimmed.Length is > 0 and <= 4
                && int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out part);
        }

        /// <summary>
        /// An answer as text, the way a conditional or a cross-field rule reads
        /// it: a missing or null answer is "", a boolean is "true"/"false", a
        /// number is its invariant text.
        /// </summary>
        private static string AnswerText(JsonElement answers, string? key)
        {
            if (key is null || !answers.TryGetProperty(key, out JsonElement value))
            {
                return string.Empty;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty,
            };
        }

        private static bool MatchesPattern(string text, string pattern)
        {
            try
            {
                // The designer wrote the pattern for the browser, so it is read
                // the way JavaScript reads it: ECMAScript mode gives `\d` and `\w`
                // their ASCII meanings. The whole answer must match, checked by
                // length because in .NET `$` also matches before a final newline,
                // which the browser's regex does not.
                Match match = Regex.Match(text, $"^(?:{pattern})$", RegexOptions.ECMAScript, PatternTimeout);
                return match.Success && match.Index == 0 && match.Length == text.Length;
            }
            catch (ArgumentException)
            {
                // Not an expression .NET can run in that mode: the designer's
                // pattern is checked by the browser alone rather than refusing
                // every submission.
                return true;
            }
            catch (RegexMatchTimeoutException)
            {
                return true;
            }
        }

        /// <summary>
        /// Whether an answer counts as not given. An unticked checkbox is
        /// <c>false</c> on the wire, and a required checkbox means "must be
        /// ticked", so for a checkbox <c>false</c> is empty too, as it is for
        /// Form.io's own required rule.
        /// </summary>
        private static bool IsEmpty(string type, JsonElement value) =>
            value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
                JsonValueKind.False => string.Equals(type, "checkbox", StringComparison.Ordinal),
                _ => false,
            };

        private static bool TypeMatches(string type, JsonElement value) =>
            type switch
            {
                "number" or "currency" => value.ValueKind == JsonValueKind.Number,
                "checkbox" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                // A choice is whatever the designer typed as the option's value.
                // Form.io sends a numeric-looking option ("10", "12") as a number
                // and "true"/"false" as a boolean, so those read as that choice
                // rather than as the wrong type; every reader takes them as text.
                "select" or "radio" or "bcgovRadio"
                    => value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False,
                // These custom client-side components carry citizen answers, so
                // unlike "bcgovAccordion" they are data fields and are expected as
                // strings, like the built-in fields they extend.
                "textfield" or "textarea" or "email" or "bcgovAddressAutocomplete"
                    or "phoneNumber" or "day" or "datetime" or "sin" or "phn" or "password"
                    => value.ValueKind == JsonValueKind.String,
                _ => true,
            };

        private static ValidationErrorModel Error(string field, string keyword, string message) =>
            new() { Field = field, Keyword = keyword, Message = message };

        /// <summary>
        /// A failure worded from the form when it authored wording for the rule,
        /// otherwise from the compiled default (which the catalogue may replace).
        /// </summary>
        private static ValidationErrorModel RuleError(ComponentInfo component, string rule, string keyword, string defaultMessage)
        {
            string? authored = component.AuthoredMessage(rule);
            return new ValidationErrorModel
            {
                Field = component.Key,
                Keyword = keyword,
                Message = authored ?? defaultMessage,
                HasAuthoredMessage = authored is not null,
            };
        }

        /// <summary>
        /// A Form.io simple conditional: show (or hide) the component when the
        /// answer to <c>when</c> equals <c>eq</c>. Compared as text, the way
        /// Form.io compares them.
        /// </summary>
        private sealed record SimpleCondition(string When, string Eq, bool Show)
        {
            public static bool TryRead(JsonElement component, out SimpleCondition condition)
            {
                condition = null!;
                if (!component.TryGetProperty("conditional", out JsonElement cond)
                    || cond.ValueKind != JsonValueKind.Object
                    || !cond.TryGetProperty("when", out JsonElement when)
                    || when.ValueKind != JsonValueKind.String
                    || when.GetString() is not { Length: > 0 } target)
                {
                    return false;
                }

                string eq = cond.TryGetProperty("eq", out JsonElement eqValue)
                    ? eqValue.ValueKind switch
                    {
                        JsonValueKind.String => eqValue.GetString() ?? string.Empty,
                        JsonValueKind.Number => eqValue.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => string.Empty,
                    }
                    : string.Empty;

                // Exactly as Form.io reads it: `String(show) === "true"`. The
                // builder leaves `show` blank unless the designer picks "Show",
                // and blank means hide-on-match, not show.
                bool show = cond.TryGetProperty("show", out JsonElement showValue)
                    && showValue.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.String => string.Equals(showValue.GetString(), "true", StringComparison.Ordinal),
                        _ => false,
                    };

                condition = new SimpleCondition(target, eq, show);
                return true;
            }

            public bool IsSatisfied(JsonElement answers)
            {
                bool equal = string.Equals(AnswerText(answers, When), Eq, StringComparison.Ordinal);
                return Show ? equal : !equal;
            }
        }

        private sealed record ComponentInfo(
            string Key,
            string Type,
            bool Required,
            string? Validator,
            string? MatchesKey,
            string? DatePartsMonthKey,
            string? DatePartsYearKey,
            string? Pattern,
            int? MinLength,
            int? MaxLength,
            decimal? Min,
            decimal? Max,
            IReadOnlyDictionary<string, string> Errors,
            string? CustomMessage,
            string? PatternMessage,
            bool KeepsValueWhenHidden,
            bool ValidateWhenHidden,
            IReadOnlyList<SimpleCondition> Conditions)
        {
            /// <summary>
            /// The rules Form.io itself runs in the browser. Their wording follows
            /// Form.io's precedence so the inline message and the 422 agree.
            /// </summary>
            private static readonly HashSet<string> StockRules =
                ["required", "pattern", "minLength", "maxLength", "min", "max"];

            /// <summary>Whether every conditional above and on this field shows it.</summary>
            public bool IsVisible(JsonElement answers)
            {
                foreach (SimpleCondition condition in Conditions)
                {
                    if (!condition.IsSatisfied(answers))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// The form's wording for a rule on this field, in the order the
            /// browser shows it. For Form.io's own rules that is the catch-all
            /// <c>validate.customMessage</c> first, then <c>validate.patternMessage</c>
            /// for the pattern, then the <c>errors</c> map; for the named domain
            /// rules the browser's wrappers prefer the <c>errors</c> entry for the
            /// rule, so the catch-all cannot word a date failure as "required".
            /// </summary>
            public string? AuthoredMessage(string rule)
            {
                string? fromMap = Errors.TryGetValue(rule, out string? message) ? message : null;
                if (StockRules.Contains(rule))
                {
                    return CustomMessage
                        ?? (string.Equals(rule, "pattern", StringComparison.Ordinal) ? PatternMessage : null)
                        ?? fromMap;
                }

                return fromMap ?? CustomMessage;
            }
        }
    }
}
