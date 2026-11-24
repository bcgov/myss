namespace Myss.FormSpec
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Resolves a form for a request channel. A component whose channel rule has
    /// <c>show: false</c> is removed together with everything inside it, a rule's
    /// <c>required</c> becomes <c>validate.required</c>, and every
    /// <c>properties.myssChannels</c> is dropped, so the result is an ordinary form
    /// that is served and validated like any other. The form editor's preview has a
    /// TypeScript copy; both run <c>Shared/validation/channel-specs.json</c>.
    /// </summary>
    public static class ChannelSpec
    {
        private const string RulesProperty = "myssChannels";

        /// <summary>The form as it applies to <paramref name="channel"/>.</summary>
        /// <param name="spec">The form spec as published, channel rules included.</param>
        /// <param name="channel">One of <see cref="RequestChannels.All"/>.</param>
        /// <returns>A new spec that does not depend on <paramref name="spec"/>'s document.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The channel is not a request channel.</exception>
        public static JsonElement Resolve(JsonElement spec, string channel)
        {
            if (!RequestChannels.IsKnown(channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Not a request channel.");
            }

            JsonNode? root = JsonNode.Parse(spec.GetRawText());
            return ResolveChildren(root, channel) ? JsonSerializer.SerializeToElement(root) : spec.Clone();
        }

        /// <summary>Resolves everything below a node; true when anything changed.</summary>
        private static bool ResolveChildren(JsonNode? node, string channel)
        {
            if (node is not JsonObject container)
            {
                return false;
            }

            bool changed = ResolveComponents(container["components"] as JsonArray, channel);
            changed |= ResolveColumns(container["columns"] as JsonArray, channel);
            changed |= ResolveRows(container["rows"] as JsonArray, channel);
            return changed;
        }

        private static bool ResolveComponents(JsonArray? components, string channel)
        {
            if (components is null)
            {
                return false;
            }

            bool changed = false;
            for (int i = components.Count - 1; i >= 0; i--)
            {
                if (components[i] is not JsonObject component)
                {
                    continue;
                }

                if (Setting(component, channel, "show") is false)
                {
                    components.RemoveAt(i);
                    changed = true;
                    continue;
                }

                changed |= ApplyRules(component, channel);
                changed |= ResolveChildren(component, channel);
            }

            return changed;
        }

        /// <summary><c>columns</c> layout: each column carries its own <c>components</c>.</summary>
        private static bool ResolveColumns(JsonArray? columns, string channel)
        {
            if (columns is null)
            {
                return false;
            }

            bool changed = false;
            foreach (JsonNode? column in columns)
            {
                changed |= ResolveChildren(column, channel);
            }

            return changed;
        }

        /// <summary><c>rows</c> (table layout): a row is an array of cells, each with <c>components</c>.</summary>
        private static bool ResolveRows(JsonArray? rows, string channel)
        {
            if (rows is null)
            {
                return false;
            }

            bool changed = false;
            foreach (JsonArray cells in rows.OfType<JsonArray>())
            {
                foreach (JsonNode? cell in cells)
                {
                    changed |= ResolveChildren(cell, channel);
                }
            }

            return changed;
        }

        /// <summary>Applies the channel's <c>required</c> and drops the rules; true when the component had rules.</summary>
        private static bool ApplyRules(JsonObject component, string channel)
        {
            if (component["properties"] is not JsonObject properties || !properties.ContainsKey(RulesProperty))
            {
                return false;
            }

            if (Setting(component, channel, "required") is bool required)
            {
                if (component["validate"] is not JsonObject validate)
                {
                    validate = new JsonObject();
                    component["validate"] = validate;
                }

                validate["required"] = required;
            }

            properties.Remove(RulesProperty);
            return true;
        }

        /// <summary>A channel rule's setting, when it is given as true or false.</summary>
        private static bool? Setting(JsonObject component, string channel, string name) =>
            component["properties"] is JsonObject properties
            && properties[RulesProperty] is JsonObject rules
            && rules[channel] is JsonObject rule
            && rule[name] is JsonValue value
            && (value.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
                ? value.GetValue<bool>()
                : null;
    }
}
