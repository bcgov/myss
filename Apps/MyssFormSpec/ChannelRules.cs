namespace Myss.FormSpec
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// The structure rule for channel rules (<c>properties.myssChannels</c>), run by
    /// <see cref="FormSpecValidator.ValidateSpecStructure"/>. A component's rules are an
    /// object keyed by known channels, each setting only <c>show</c> and
    /// <c>required</c>, as true or false; and a component shown in a channel does not
    /// depend on a component hidden in it: through <c>conditional.when</c>,
    /// <c>myssMatches</c>, a day's <c>myssDateParts</c>, or as a repeat section's count
    /// question. The wording is shared with the two TypeScript
    /// copies through <c>Shared/validation/channel-specs.json</c>.
    /// </summary>
    internal static class ChannelRules
    {
        private const string RulesProperty = "myssChannels";

        private const string RepeatSectionType = "bcgovRepeatSection";

        private static readonly string[] Settings = ["show", "required"];

        /// <summary>Every channel rule problem in the spec, component by component in document order.</summary>
        /// <param name="spec">The Form.io spec body.</param>
        /// <returns>The problems, each named by its component's key.</returns>
        internal static List<ValidationErrorModel> Errors(JsonElement spec)
        {
            List<Placed> placed = [];
            Place(spec, new HashSet<string>(StringComparer.Ordinal), placed);

            Dictionary<string, Placed> byKey = new(StringComparer.Ordinal);
            foreach (Placed item in placed)
            {
                if (item.Key is not null)
                {
                    byKey.TryAdd(item.Key, item);
                }
            }

            // A keyless component is left to the key rule.
            List<ValidationErrorModel> errors = [];
            foreach (Placed item in placed.Where(p => p.Key is not null))
            {
                foreach (string message in RuleProblems(item.Key!, item.Component).Concat(DependencyProblems(item, byKey)))
                {
                    errors.Add(new ValidationErrorModel
                    {
                        Field = item.Key!,
                        Keyword = FormSpecStructureKeywords.ChannelRulesInvalid,
                        Message = message,
                    });
                }
            }

            return errors;
        }

        private static void Place(JsonElement node, IReadOnlySet<string> hiddenAbove, List<Placed> into)
        {
            foreach (JsonElement component in Children(node, "components"))
            {
                if (component.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                IReadOnlySet<string> hiddenIn = HiddenIn(component, hiddenAbove);
                into.Add(new Placed(component, Key(component), hiddenIn));
                Place(component, hiddenIn, into);
            }

            foreach (JsonElement column in Children(node, "columns"))
            {
                Place(column, hiddenAbove, into);
            }

            foreach (JsonElement row in Children(node, "rows"))
            {
                if (row.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement cell in row.EnumerateArray())
                    {
                        Place(cell, hiddenAbove, into);
                    }
                }
            }
        }

        private static IEnumerable<JsonElement> Children(JsonElement node, string name) =>
            node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty(name, out JsonElement children)
            && children.ValueKind == JsonValueKind.Array
                ? children.EnumerateArray()
                : [];

        /// <summary>The channels a component is hidden in: its container's, plus those its own rules hide it from.</summary>
        private static HashSet<string> HiddenIn(JsonElement component, IReadOnlySet<string> hiddenAbove)
        {
            HashSet<string> hidden = new(hiddenAbove, StringComparer.Ordinal);
            if (!TryGetRules(component, out JsonElement rules) || rules.ValueKind != JsonValueKind.Object)
            {
                return hidden;
            }

            foreach (string channel in RequestChannels.All)
            {
                if (rules.TryGetProperty(channel, out JsonElement rule)
                    && rule.ValueKind == JsonValueKind.Object
                    && rule.TryGetProperty("show", out JsonElement show)
                    && show.ValueKind == JsonValueKind.False)
                {
                    hidden.Add(channel);
                }
            }

            return hidden;
        }

        private static IEnumerable<string> RuleProblems(string key, JsonElement component)
        {
            if (!TryGetRules(component, out JsonElement rules))
            {
                return [];
            }

            if (rules.ValueKind != JsonValueKind.Object)
            {
                return [$"The channel rules of \"{key}\" must be an object keyed by channel."];
            }

            return rules.EnumerateObject().SelectMany(rule => OneRuleProblems(key, rule));
        }

        private static IEnumerable<string> OneRuleProblems(string key, JsonProperty rule)
        {
            if (!RequestChannels.IsKnown(rule.Name))
            {
                yield return $"The channel rules of \"{key}\" name \"{rule.Name}\", which is not a channel.";
                yield break;
            }

            if (rule.Value.ValueKind != JsonValueKind.Object)
            {
                yield return $"The channel rule of \"{key}\" for \"{rule.Name}\" must be an object.";
                yield break;
            }

            foreach (JsonProperty setting in rule.Value.EnumerateObject())
            {
                if (!Settings.Contains(setting.Name, StringComparer.Ordinal))
                {
                    yield return $"The channel rule of \"{key}\" for \"{rule.Name}\" has \"{setting.Name}\"; only show and required are allowed.";
                }
                else if (setting.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    yield return $"The channel rule of \"{key}\" for \"{rule.Name}\" must set \"{setting.Name}\" to true or false.";
                }
            }
        }

        private static IEnumerable<string> DependencyProblems(Placed item, Dictionary<string, Placed> byKey)
        {
            List<string> targets = DependencyTargets(item.Component);
            string? count = string.Equals(ReadString(item.Component, "type"), RepeatSectionType, StringComparison.Ordinal)
                ? RepeatCount(item.Component)
                : null;

            foreach (string channel in RequestChannels.All.Where(c => !item.HiddenIn.Contains(c)))
            {
                foreach (string target in targets.Where(t => IsHiddenIn(t, channel, byKey)))
                {
                    yield return $"\"{item.Key}\" depends on \"{target}\", which is hidden for the {channel} channel.";
                }

                if (IsHiddenIn(count, channel, byKey))
                {
                    yield return $"The repeat section \"{item.Key}\" counts with \"{count}\", which is hidden for the {channel} channel.";
                }
            }
        }

        private static bool IsHiddenIn(string? key, string channel, Dictionary<string, Placed> byKey) =>
            key is not null && byKey.TryGetValue(key, out Placed? found) && found.HiddenIn.Contains(channel);

        /// <summary>The rules, when the component has any; <c>null</c> counts as none.</summary>
        private static bool TryGetRules(JsonElement component, out JsonElement rules)
        {
            rules = default;
            return component.TryGetProperty("properties", out JsonElement properties)
                && properties.ValueKind == JsonValueKind.Object
                && properties.TryGetProperty(RulesProperty, out rules)
                && rules.ValueKind != JsonValueKind.Null;
        }

        /// <summary>
        /// The components this one reads, each once: its conditional's field, the email it
        /// confirms, and, for a day, its month and year.
        /// </summary>
        private static List<string> DependencyTargets(JsonElement component)
        {
            JsonElement properties = component.TryGetProperty("properties", out JsonElement p) ? p : default;
            JsonElement dateParts = ReadString(properties, "myssValidator") == "dateParts"
                && properties.TryGetProperty("myssDateParts", out JsonElement parts)
                    ? parts
                    : default;

            string?[] targets =
            [
                ConditionalTarget(component),
                ReadString(properties, "myssMatches"),
                ReadString(dateParts, "month"),
                ReadString(dateParts, "year"),
            ];
            return [.. targets.OfType<string>().Distinct(StringComparer.Ordinal)];
        }

        private static string? ConditionalTarget(JsonElement component) =>
            component.TryGetProperty("conditional", out JsonElement conditional)
                ? ReadString(conditional, "when")
                : null;

        private static string? RepeatCount(JsonElement section)
        {
            string? count = section.TryGetProperty("properties", out JsonElement properties)
                ? ReadString(properties, "myssRepeatCount")
                : null;
            return string.IsNullOrWhiteSpace(count) ? null : count;
        }

        private static string? Key(JsonElement component)
        {
            string? key = ReadString(component, "key");
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }

        private static string? ReadString(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
                ? text
                : null;

        /// <summary>A component with its key and the channels it is hidden in.</summary>
        private sealed record Placed(JsonElement Component, string? Key, IReadOnlySet<string> HiddenIn);
    }
}
