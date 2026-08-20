namespace Myss.Api.Services
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Myss.Api.Domain;
    using Myss.Api.Models;

    /// <summary>
    /// Validates a submitted answers object against the Form.io spec version it
    /// claims to have been rendered with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handbook is unambiguous that client-side validation is a UX
    /// convenience and never a security control: "the back-end re-validates
    /// every value regardless of what the form said". This class is that
    /// re-validation. It is pure — spec in, answers in, failures out — so it can
    /// be exercised without a database, an HTTP request or a browser.
    /// </para>
    /// <para><b>How a field opts in to a domain rule.</b> Form.io has no notion
    /// of a Canadian SIN, so the spec must say which rule applies. Two ways,
    /// checked in order:</para>
    /// <list type="number">
    /// <item><description>An explicit marker in the component's
    /// <c>properties</c> map: <c>{ "myssValidator": "sin" }</c>. Form.io's
    /// <c>properties</c> is free-form key-value, so this is authored as
    /// ordinary content on an ordinary textfield — no code, no deployment.
    /// A confirmation field adds <c>{ "myssMatches": "contactEmail" }</c>.</description></item>
    /// <item><description>The component <c>type</c> itself, for the custom
    /// components Phase 1 will introduce (<c>sin</c>), and for Form.io's own
    /// <c>email</c> type.</description></item>
    /// </list>
    /// <para>
    /// The marker route exists so this slice can be proved against a form built
    /// entirely from stock components, before the custom component library
    /// exists. It stays useful afterwards for one-off patterned fields that do
    /// not justify a component of their own.
    /// </para>
    /// <para><b>Known gap — conditionally required fields.</b> A field carrying a
    /// <c>conditional</c> is exempt from the required check, because whether it
    /// is required depends on answers to other questions, and evaluating
    /// Form.io's conditional logic server-side is most of option C in §7.2 of
    /// the assessment. Deliberately out of scope here; it must be addressed
    /// before a form with conditionally-required sections (Phase 2's
    /// demonstration form has several) can be trusted. The field is still type-
    /// and domain-checked when an answer is present.</para>
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
        ];

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
                Collect(spec, components, tolerated);
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
                bool present = answers.TryGetProperty(key, out JsonElement value) && !IsEmpty(value);

                if (!present)
                {
                    if (component.Required && !component.IsConditional)
                    {
                        errors.Add(Error(key, ValidationKeywords.FieldRequired, "This answer is required."));
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

                ApplyDomainRules(key, component, value, answers, errors);
            }

            return errors;
        }

        private static void ApplyDomainRules(
            string key,
            ComponentInfo component,
            JsonElement value,
            JsonElement answers,
            List<ValidationErrorModel> errors)
        {
            string? rule = component.Validator;
            if (rule is null)
            {
                return;
            }

            string raw = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

            switch (rule)
            {
                case "sin":
                    DomainValidationResult<Sin> sin = Sin.TryCreate(raw);
                    if (!sin.IsValid)
                    {
                        errors.Add(Error(key, sin.Keyword!, sin.Message!));
                    }

                    break;

                case "email":
                    DomainValidationResult<EmailAddress> email = EmailAddress.TryCreate(raw);
                    if (!email.IsValid)
                    {
                        errors.Add(Error(key, email.Keyword!, email.Message!));
                    }

                    break;
            }

            // A confirmation field names the field it confirms. The failure is
            // reported against the confirmation, which is where the citizen's
            // focus should land.
            if (component.MatchesKey is not null)
            {
                string other = answers.TryGetProperty(component.MatchesKey, out JsonElement o) && o.ValueKind == JsonValueKind.String
                    ? o.GetString() ?? string.Empty
                    : string.Empty;

                if (!EmailAddress.ConfirmationMatches(other, raw))
                {
                    errors.Add(Error(
                        key,
                        ValidationKeywords.EmailMismatch,
                        "The two email addresses do not match."));
                }
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
        private static void Collect(
            JsonElement node,
            Dictionary<string, ComponentInfo> into,
            HashSet<string> tolerated)
        {
            if (node.TryGetProperty("components", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in children.EnumerateArray())
                {
                    Visit(child, into, tolerated);
                }
            }

            if (node.TryGetProperty("columns", out JsonElement columns) && columns.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement column in columns.EnumerateArray())
                {
                    Collect(column, into, tolerated);
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
                        Collect(cell, into, tolerated);
                    }
                }
            }
        }

        private static void Visit(
            JsonElement component,
            Dictionary<string, ComponentInfo> into,
            HashSet<string> tolerated)
        {
            if (component.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            string type = component.TryGetProperty("type", out JsonElement t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? string.Empty
                : string.Empty;

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
                    into.TryAdd(key, Describe(key, type, component));
                }
            }

            // Recurse regardless: a panel is not a field but holds them.
            Collect(component, into, tolerated);
        }

        private static ComponentInfo Describe(string key, string type, JsonElement component)
        {
            bool required = component.TryGetProperty("validate", out JsonElement validate)
                && validate.ValueKind == JsonValueKind.Object
                && validate.TryGetProperty("required", out JsonElement req)
                && req.ValueKind == JsonValueKind.True;

            bool conditional = component.TryGetProperty("conditional", out JsonElement cond)
                && cond.ValueKind == JsonValueKind.Object
                && cond.TryGetProperty("when", out JsonElement when)
                && when.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(when.GetString());

            string? validator = null;
            string? matches = null;

            if (component.TryGetProperty("properties", out JsonElement props) && props.ValueKind == JsonValueKind.Object)
            {
                if (props.TryGetProperty("myssValidator", out JsonElement v) && v.ValueKind == JsonValueKind.String)
                {
                    validator = v.GetString();
                }

                if (props.TryGetProperty("myssMatches", out JsonElement m) && m.ValueKind == JsonValueKind.String)
                {
                    matches = m.GetString();
                }
            }

            // Fall back to the component type, which is how the Phase 1 custom
            // components and Form.io's own email type will declare themselves.
            validator ??= type switch
            {
                "sin" => "sin",
                "email" => "email",
                _ => null,
            };

            return new ComponentInfo(key, type, required, conditional, validator, matches);
        }

        private static bool IsEmpty(JsonElement value) =>
            value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
                _ => false,
            };

        private static bool TypeMatches(string type, JsonElement value) =>
            type switch
            {
                "number" or "currency" => value.ValueKind == JsonValueKind.Number,
                "checkbox" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "textfield" or "textarea" or "email" or "select" or "radio"
                    or "phoneNumber" or "day" or "datetime" or "sin" or "phn" or "password"
                    => value.ValueKind == JsonValueKind.String,
                _ => true,
            };

        private static ValidationErrorModel Error(string field, string keyword, string message) =>
            new() { Field = field, Keyword = keyword, Message = message };

        private sealed record ComponentInfo(
            string Key,
            string Type,
            bool Required,
            bool IsConditional,
            string? Validator,
            string? MatchesKey);
    }
}
