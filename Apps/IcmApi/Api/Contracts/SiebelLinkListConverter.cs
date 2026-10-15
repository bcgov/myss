namespace Icm.Api.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Reads a <c>Link</c> property that is an array on a read and a single object on a
    /// write: MEASURED on SIT2 on 2026-09-29, the record a POST returns carries
    /// <c>"Link": { "rel": "self", … }</c> where a GET's carries <c>"Link": [ … ]</c>.
    /// Either shape becomes a list, so one wire type serves both.
    /// </summary>
    internal sealed class SiebelLinkListConverter : JsonConverter<IList<SiebelLink>?>
    {
        /// <inheritdoc/>
        public override IList<SiebelLink>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.StartObject:
                    SiebelLink? single = JsonSerializer.Deserialize<SiebelLink>(ref reader, options);
                    return single is null ? [] : [single];
                case JsonTokenType.StartArray:
                    return JsonSerializer.Deserialize<List<SiebelLink>>(ref reader, options);
                default:
                    throw new JsonException($"A Link must be an object or an array, not {reader.TokenType}.");
            }
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, IList<SiebelLink>? value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, options);
    }
}
