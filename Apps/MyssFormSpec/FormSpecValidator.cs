namespace Myss.FormSpec
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Myss.FormSpec.Domain;

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
    /// <para><b>Repeat sections.</b> A <c>bcgovRepeatSection</c>'s answer is a
    /// list of rows, one per unit of the number question named by
    /// <c>properties.myssRepeatCount</c>, at most <c>properties.myssRepeatMax</c>
    /// (10 when unset). Each row is checked against the section's fields, and
    /// a failure in a row is named by its path, such as <c>children[1].firstName</c>.
    /// The section shows only while its count question does, and the count
    /// question is required to be a whole number from 1 to the maximum, as the
    /// form treats it.</para>
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
    /// MyssApi's <c>ErrorMessageResolver</c> swaps for the catalogue's wording.</para>
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

            // Custom BC Gov display components, rendered client-side by a
            // registered Form.io component. They carry no citizen answer, so treat
            // them like the other non-data content components. A callout and an
            // inline alert are containers: the fields inside them are still
            // collected, because the walk recurses into every component's children.
            "bcgovAccordion",
            "bcgovCallout",
            "bcgovInlineAlert",
        ];

        /// <summary>
        /// A spec pattern is a regular expression the designer typed; a slow one
        /// must not hold a request, and one that is not a .NET expression at all
        /// is skipped rather than refused.
        /// </summary>
        private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

        /// <summary>A section of questions repeated once per unit of a count answer.</summary>
        private const string RepeatSectionType = "bcgovRepeatSection";

        /// <summary>The most rows a repeat section takes when its spec sets no maximum.</summary>
        private const int DefaultRepeatMax = 10;

        /// <summary>The highest maximum a repeat section may set.</summary>
        private const int RepeatMaxCeiling = 50;

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

            FieldSet fields = new();
            if (spec.ValueKind == JsonValueKind.Object)
            {
                Collect(spec, fields, []);
                ApplyCountRules(fields);
            }

            ValidateFields(fields, new AnswerScope(answers, answers, Visible: true), errors);
            return OnePerField(errors);
        }

        /// <summary>
        /// Checks one answers object, the whole submission or one row of a
        /// repeat section, against the fields it may hold.
        /// </summary>
        private static void ValidateFields(FieldSet fields, AnswerScope scope, List<ValidationErrorModel> errors)
        {
            // Unknown keys first: an answer the spec has no field for is either a
            // client bug or someone probing the endpoint. Either way it must not
            // be persisted silently.
            foreach (JsonProperty answer in scope.Row.EnumerateObject())
            {
                if (!fields.Components.ContainsKey(answer.Name) && !fields.Tolerated.Contains(answer.Name))
                {
                    errors.Add(Error(
                        answer.Name,
                        ValidationKeywords.FieldUnknown,
                        $"\"{answer.Name}\" is not a field on this form."));
                }
            }

            foreach (ComponentInfo component in fields.Components.Values)
            {
                if (component.Repeat is RepeatSection section)
                {
                    ValidateRepeatSection(component, section, scope, errors);
                }
                else
                {
                    ValidateField(component, scope, errors);
                }
            }
        }

        private static void ValidateField(ComponentInfo component, AnswerScope scope, List<ValidationErrorModel> errors)
        {
            bool visible = scope.Visible && component.IsVisible(scope);
            bool present = scope.Row.TryGetProperty(component.Key, out JsonElement value) && !IsEmpty(component.Type, value);

            if (!present)
            {
                if (component.Required && visible)
                {
                    errors.Add(RequiredError(component));
                }

                // An empty confirmation of a filled partner is a mismatch, not
                // a blank: the typo guard must not be satisfiable by leaving
                // the second field out.
                if (component.MatchesKey is not null && visible && scope.Text(component.MatchesKey).Trim().Length > 0)
                {
                    errors.Add(RuleError(component, "matches", ValidationKeywords.EmailMismatch, "The two email addresses do not match."));
                }

                return;
            }

            if (!AnswerTypeMatches(component, value))
            {
                errors.Add(WrongType(component.Key));
                return;
            }

            // A hidden field's value is normally cleared by the form, so one
            // that arrives anyway did not come from a citizen and is held to
            // the field's rules rather than stored unchecked. The exception
            // is a field the designer set to keep its value when hidden: the
            // form keeps and submits that value without validating it, and
            // the citizen cannot see the field to correct it.
            if (!visible && component.KeepsValueWhenHidden && !component.ValidateWhenHidden)
            {
                return;
            }

            ApplyFormatRules(component, value, errors);
            ApplyDomainRules(component, value, scope, errors);
        }

        /// <summary>
        /// A repeat section's answer is a list of rows, one per unit of its
        /// count answer. Each row is checked like a small form of its own, and
        /// a failure in it is named by its path, <c>children[1].firstName</c>.
        /// </summary>
        private static void ValidateRepeatSection(
            ComponentInfo component,
            RepeatSection section,
            AnswerScope scope,
            List<ValidationErrorModel> errors)
        {
            bool visible = scope.Visible && component.IsVisible(scope);

            if (!scope.Row.TryGetProperty(component.Key, out JsonElement rows) || rows.ValueKind == JsonValueKind.Null)
            {
                if (visible)
                {
                    CheckRowCount(component, section, 0, scope, errors);
                }

                return;
            }

            // The form never draws more rows than the maximum, so a longer list
            // did not come from it and is refused even on a draft save.
            if (!IsRowList(rows, section.Max))
            {
                errors.Add(WrongType(component.Key));
                return;
            }

            if (visible)
            {
                CheckRowCount(component, section, rows.GetArrayLength(), scope, errors);
            }

            int index = 0;
            foreach (JsonElement row in rows.EnumerateArray())
            {
                List<ValidationErrorModel> rowErrors = [];
                ValidateFields(section.Fields, new AnswerScope(row, scope.Root, visible), rowErrors);
                foreach (ValidationErrorModel error in rowErrors)
                {
                    error.Field = $"{component.Key}[{index}].{error.Field}";
                    errors.Add(error);
                }

                index++;
            }
        }

        /// <summary>
        /// The rows must number exactly what the count answer says. A count the
        /// section cannot draw, or a missing one, is reported on the count
        /// question itself; with no count question in the form, no count means
        /// no rows.
        /// </summary>
        private static void CheckRowCount(
            ComponentInfo component,
            RepeatSection section,
            int rowCount,
            AnswerScope scope,
            List<ValidationErrorModel> errors)
        {
            if (section.CountKey is null)
            {
                return;
            }

            int expected = 0;
            bool usable = scope.TryGet(section.CountKey, out JsonElement count)
                && !IsEmpty("number", count)
                && TryReadWholeNumber(count, out expected)
                && expected >= 1
                && expected <= section.Max;
            if (!usable)
            {
                if (section.HasCountQuestion)
                {
                    return;
                }

                expected = 0;
            }

            if (rowCount != expected)
            {
                errors.Add(RuleError(
                    component,
                    "rowCount",
                    ValidationKeywords.FieldRowCountMismatch,
                    "The number of entries does not match the number you entered."));
            }
        }

        /// <summary>
        /// A repeat section shows only while its count question does, and the
        /// count question takes the limits of what the section can draw: an
        /// answer is required, and it is a whole number from 1 to the section's
        /// maximum (or the question's own narrower limits).
        /// </summary>
        private static void ApplyCountRules(FieldSet fields)
        {
            List<ComponentInfo> components = [.. fields.Components.Values];
            foreach (ComponentInfo section in components)
            {
                if (section.Repeat?.CountKey is not string countKey
                    || !fields.Components.TryGetValue(countKey, out ComponentInfo? count)
                    || !string.Equals(count.Type, "number", StringComparison.Ordinal))
                {
                    continue;
                }

                int max = section.Repeat.Max;
                fields.Components[countKey] = count with
                {
                    Required = true,
                    WholeNumber = true,
                    Min = count.Min > 1 ? count.Min : 1,
                    Max = count.Max < max ? count.Max : max,
                };
                fields.Components[section.Key] = section with
                {
                    Conditions = [.. section.Conditions, .. count.Conditions],
                    Repeat = section.Repeat with { HasCountQuestion = true },
                };
            }
        }

        /// <summary>The JSON type the field takes; a count question also takes no fraction.</summary>
        private static bool AnswerTypeMatches(ComponentInfo component, JsonElement value) =>
            TypeMatches(component.Type, value) && (!component.WholeNumber || IsWholeNumber(value));

        private static bool IsWholeNumber(JsonElement value) =>
            value.TryGetDecimal(out decimal number) && number == decimal.Truncate(number);

        private static bool IsRowList(JsonElement value, int max)
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > max)
            {
                return false;
            }

            foreach (JsonElement row in value.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }
            }

            return true;
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

            errors.AddRange(RepeatSectionErrors(components));
            return errors;
        }

        /// <summary>
        /// Each repeat section names a number question that is not hidden,
        /// outside every repeat section, as its count, sets a maximum from 1 to
        /// 50 if it sets one, and holds no repeat section of its own. A keyless section is left to the key rule. The
        /// wording is shared with the two TypeScript copies of this rule through
        /// <c>Shared/validation/repeat-section-specs.json</c>.
        /// </summary>
        private static List<ValidationErrorModel> RepeatSectionErrors(List<JsonElement> components)
        {
            Dictionary<string, JsonElement> questionsByKey = new(StringComparer.Ordinal);
            foreach (JsonElement component in components)
            {
                if (ReadString(component, "key") is string key)
                {
                    questionsByKey.TryAdd(key, component);
                }
            }

            List<(string Key, JsonElement Section)> sections = [];
            foreach (JsonElement component in components)
            {
                string? key = ReadString(component, "key");
                if (string.Equals(ReadString(component, "type"), RepeatSectionType, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(key))
                {
                    sections.Add((key, component));
                }
            }

            Dictionary<string, string> owners = SectionsByInnerKey(sections);
            List<ValidationErrorModel> errors = [];
            foreach ((string key, JsonElement section) in sections)
            {
                foreach (string message in RepeatSectionProblems(key, section, questionsByKey, owners))
                {
                    errors.Add(Error(key, FormSpecStructureKeywords.RepeatSectionInvalid, message));
                }
            }

            return errors;
        }

        /// <summary>The repeat section each question inside one belongs to, by the question's key.</summary>
        private static Dictionary<string, string> SectionsByInnerKey(List<(string Key, JsonElement Section)> sections)
        {
            Dictionary<string, string> owners = new(StringComparer.Ordinal);
            foreach ((string key, JsonElement section) in sections)
            {
                List<JsonElement> inside = [];
                CollectAll(section, inside);
                foreach (JsonElement component in inside)
                {
                    if (ReadString(component, "key") is string innerKey)
                    {
                        owners.TryAdd(innerKey, key);
                    }
                }
            }

            return owners;
        }

        private static List<string> RepeatSectionProblems(
            string key,
            JsonElement section,
            Dictionary<string, JsonElement> questionsByKey,
            Dictionary<string, string> owners)
        {
            List<JsonElement> inside = [];
            CollectAll(section, inside);
            HashSet<string> insideKeys = new(StringComparer.Ordinal);
            foreach (JsonElement component in inside)
            {
                if (ReadString(component, "key") is string insideKey)
                {
                    insideKeys.Add(insideKey);
                }
            }

            JsonElement properties = section.TryGetProperty("properties", out JsonElement p) ? p : default;
            List<string> problems = [];

            if (CountQuestionProblem(key, ReadString(properties, "myssRepeatCount"), questionsByKey, insideKeys, owners) is string countProblem)
            {
                problems.Add(countProblem);
            }

            if (MaximumProblem(key, properties) is string maximumProblem)
            {
                problems.Add(maximumProblem);
            }

            foreach (JsonElement component in inside)
            {
                if (string.Equals(ReadString(component, "type"), RepeatSectionType, StringComparison.Ordinal))
                {
                    problems.Add($"The repeat section \"{key}\" contains another repeat section, \"{ReadString(component, "key")}\".");
                    break;
                }
            }

            return problems;
        }

        private static string? CountQuestionProblem(
            string key,
            string? count,
            Dictionary<string, JsonElement> questionsByKey,
            HashSet<string> insideKeys,
            Dictionary<string, string> owners)
        {
            if (string.IsNullOrWhiteSpace(count))
            {
                return $"The repeat section \"{key}\" needs a count question.";
            }

            if (!questionsByKey.TryGetValue(count, out JsonElement question))
            {
                return $"The repeat section \"{key}\" counts with \"{count}\", which is not a field in this form.";
            }

            if (insideKeys.Contains(count))
            {
                return $"The repeat section \"{key}\" counts with \"{count}\", which is inside the section.";
            }

            if (owners.TryGetValue(count, out string? owner))
            {
                return $"The repeat section \"{key}\" counts with \"{count}\", which is inside the repeat section \"{owner}\".";
            }

            if (!string.Equals(ReadString(question, "type"), "number", StringComparison.Ordinal))
            {
                return $"The repeat section \"{key}\" counts with \"{count}\", which is not a number question.";
            }

            return question.TryGetProperty("hidden", out JsonElement hidden) && hidden.ValueKind == JsonValueKind.True
                ? $"The repeat section \"{key}\" counts with \"{count}\", which is hidden."
                : null;
        }

        /// <summary>A blank maximum means the default; otherwise a whole number from 1 to 50, as a number or as digits.</summary>
        private static string? MaximumProblem(string key, JsonElement properties)
        {
            if (properties.ValueKind != JsonValueKind.Object
                || !properties.TryGetProperty("myssRepeatMax", out JsonElement maximum)
                || maximum.ValueKind == JsonValueKind.Null
                || (maximum.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(maximum.GetString())))
            {
                return null;
            }

            decimal value = 0;
            bool parsed = maximum.ValueKind switch
            {
                JsonValueKind.Number => maximum.TryGetDecimal(out value),
                JsonValueKind.String => decimal.TryParse(maximum.GetString()!.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value),
                _ => false,
            };

            return parsed && value == decimal.Truncate(value) && value >= 1 && value <= RepeatMaxCeiling
                ? null
                : $"The repeat section \"{key}\" has a maximum of {maximum.GetRawText()}; use a whole number from 1 to {RepeatMaxCeiling}.";
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
            AnswerScope scope,
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
                        && TryReadDatePart(scope.Text(component.DatePartsMonthKey), out int month)
                        && TryReadDatePart(scope.Text(component.DatePartsYearKey), out int year)
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
                && !EmailAddress.ConfirmationMatches(scope.Text(component.MatchesKey), raw))
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
        /// <param name="into">Accumulator for the fields and the tolerated keys.</param>
        /// <param name="conditions">The conditionals on every container above this node.</param>
        private static void Collect(
            JsonElement node,
            FieldSet into,
            IReadOnlyList<SimpleCondition> conditions)
        {
            if (node.TryGetProperty("components", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in children.EnumerateArray())
                {
                    Visit(child, into, conditions);
                }
            }

            if (node.TryGetProperty("columns", out JsonElement columns) && columns.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement column in columns.EnumerateArray())
                {
                    Collect(column, into, conditions);
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
                        Collect(cell, into, conditions);
                    }
                }
            }
        }

        private static void Visit(
            JsonElement component,
            FieldSet into,
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

            string? key = ReadString(component, "key");

            // A repeat section's fields are answered inside each of its rows,
            // not at this level, so they are collected apart.
            if (string.Equals(type, RepeatSectionType, StringComparison.Ordinal))
            {
                if (key is not null)
                {
                    into.Components.TryAdd(key, Describe(key, type, component, conditions) with { Repeat = ReadRepeatSection(component) });
                }

                return;
            }

            if (key is not null)
            {
                if (NonDataTypes.Contains(type))
                {
                    // Form.io posts the submit button's own value back in the
                    // submission data (`"submit": true`). It is not a field, so
                    // it is neither validated nor rejected — without this, every
                    // genuine Form.io submission fails as an unknown key.
                    into.Tolerated.Add(key);
                }
                else
                {
                    into.Components.TryAdd(key, Describe(key, type, component, conditions));
                }
            }

            // Recurse regardless: a panel is not a field but holds them.
            Collect(component, into, conditions);
        }

        /// <summary>
        /// A repeat section's settings and the fields of one of its rows. A row
        /// field's conditionals start afresh at the section, whose own
        /// visibility is checked on the section.
        /// </summary>
        private static RepeatSection ReadRepeatSection(JsonElement component)
        {
            FieldSet fields = new();
            Collect(component, fields, []);

            JsonElement properties = component.TryGetProperty("properties", out JsonElement p) ? p : default;
            int? max = ReadInt(properties, "myssRepeatMax");

            return new RepeatSection(
                ReadString(properties, "myssRepeatCount"),
                max is int authored && authored >= 1 ? Math.Min(authored, RepeatMaxCeiling) : DefaultRepeatMax,
                fields);
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

            // Fall back to the component type or the phone masks on older published specs.
            validator ??= type switch
            {
                "sin" => "sin",
                "email" => "email",
                "phoneNumber" => "phone",
                "textfield" when ReadString(component, "inputMask") is "(999) 999-9999" or "999-999-9999" => "phone",
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

        /// <summary>A count as typed: a number with no fraction, zero or more.</summary>
        private static bool TryReadWholeNumber(JsonElement value, out int number)
        {
            number = 0;
            if (!TryReadNumber(value, out decimal parsed) || parsed != decimal.Truncate(parsed) || parsed < 0 || parsed > int.MaxValue)
            {
                return false;
            }

            number = (int)parsed;
            return true;
        }

        /// <summary>
        /// An answer as text, the way a conditional or a cross-field rule reads
        /// it: a null answer is "", a boolean is "true"/"false", a number is its
        /// invariant text.
        /// </summary>
        private static string AnswerText(JsonElement value) =>
            value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty,
            };

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

        private static ValidationErrorModel WrongType(string key) =>
            Error(key, ValidationKeywords.FieldWrongType, $"\"{key}\" was not submitted in the expected format.");

        private static ValidationErrorModel RequiredError(ComponentInfo component) =>
            RuleError(
                component,
                "required",
                ValidationKeywords.FieldRequired,
                component.Validator == "phone" ? "Phone number is invalid" : "This answer is required.");

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

            public bool IsSatisfied(AnswerScope scope)
            {
                bool equal = string.Equals(scope.Text(When), Eq, StringComparison.Ordinal);
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
            IReadOnlyList<SimpleCondition> Conditions,
            RepeatSection? Repeat = null,
            bool WholeNumber = false)
        {
            /// <summary>
            /// The rules Form.io itself runs in the browser. Their wording follows
            /// Form.io's precedence so the inline message and the 422 agree.
            /// </summary>
            private static readonly HashSet<string> StockRules =
                ["required", "pattern", "minLength", "maxLength", "min", "max"];

            /// <summary>Whether every conditional above and on this field shows it.</summary>
            public bool IsVisible(AnswerScope scope)
            {
                foreach (SimpleCondition condition in Conditions)
                {
                    if (!condition.IsSatisfied(scope))
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

        /// <summary>
        /// The fields one answers object may hold, the whole form's or one
        /// repeat-section row's, and the keys it may carry without validation.
        /// </summary>
        private sealed class FieldSet
        {
            public Dictionary<string, ComponentInfo> Components { get; } = [];

            public HashSet<string> Tolerated { get; } = [];
        }

        /// <param name="CountKey">The number question that sets how many rows there are.</param>
        /// <param name="Max">The most rows the section allows.</param>
        /// <param name="Fields">The fields of one row.</param>
        /// <param name="HasCountQuestion">Whether the form holds the count question, which then reports a missing count.</param>
        private sealed record RepeatSection(string? CountKey, int Max, FieldSet Fields, bool HasCountQuestion = false);

        /// <summary>
        /// Where a field's answers are read: the row of a repeat section it sits
        /// in, else the whole submission. A key the row does not hold is read
        /// from the whole submission, so a row field can depend on a question
        /// outside the section.
        /// </summary>
        /// <param name="Row">The answers object the field's own value is in.</param>
        /// <param name="Root">The whole submission.</param>
        /// <param name="Visible">False inside a repeat section a conditional hides.</param>
        private readonly record struct AnswerScope(JsonElement Row, JsonElement Root, bool Visible)
        {
            public bool TryGet(string? key, out JsonElement value)
            {
                value = default;
                return key is not null
                    && (Row.TryGetProperty(key, out value) || Root.TryGetProperty(key, out value));
            }

            /// <summary>The answer to <paramref name="key"/> as text; "" when there is none.</summary>
            public string Text(string? key) => TryGet(key, out JsonElement value) ? AnswerText(value) : string.Empty;
        }
    }
}
