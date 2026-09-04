namespace Icm.Api
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Refit;

    /// <summary>
    /// The Refit configuration the ICM APIs expect. Applied by the repositories, which are
    /// the only things that build a client.
    /// </summary>
    /// <remarks>
    /// Two settings here are correctness, not preference, and a client configured without
    /// them will misbehave in ways that are hard to spot: see <see cref="JsonOptions"/> for
    /// why nulls must not be written, and <see cref="SiebelUrlParameterFormatter"/> for why
    /// booleans must be lower-cased.
    /// </remarks>
    internal static class IcmRefitSettings
    {
        /// <summary>
        /// Gets the serializer options used for ICM request and response bodies.
        /// </summary>
        /// <remarks>
        /// <see cref="JsonIgnoreCondition.WhenWritingNull"/> is what makes a partial write
        /// possible. Every field on <see cref="Contracts.SiebelServiceRequest"/> is nullable, so
        /// without it a PUT that sets one property would also send fifty nulls and Siebel
        /// would blank fifty fields.
        /// </remarks>
        public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // Property names come from [JsonPropertyName] and contain spaces; nothing here
            // should be re-cased. PropertyNameCaseInsensitive stays on (the Web default)
            // because Siebel's casing of a field is not guaranteed to match the spec's.
            PropertyNamingPolicy = null,
        };

        /// <summary>
        /// Creates the settings for an ICM Refit client. A new instance each call, because
        /// <see cref="RefitSettings"/> is mutable and callers reasonably adjust it.
        /// </summary>
        /// <returns>The configured settings.</returns>
        public static RefitSettings Create() =>
            new(new SystemTextJsonContentSerializer(JsonOptions), new SiebelUrlParameterFormatter());
    }

    /// <summary>
    /// Formats URL parameters the way Siebel reads them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only departure from <see cref="DefaultUrlParameterFormatter"/> is booleans.
    /// <see cref="bool.ToString()"/> produces <c>"True"</c>, which Siebel does not recognise
    /// as true — it wants <c>"true"</c>. The spec bears this out: it types
    /// <c>recordcountneeded</c> as a boolean defaulting to <c>false</c> and
    /// <c>excludeEmptyFieldsInResponse</c> as a <i>string</i> defaulting to <c>"false"</c>,
    /// and both are the same lower-case literal on the wire.
    /// </para>
    /// <para>
    /// A wrong value here fails silently: Siebel treats an unrecognised flag as false and
    /// returns a perfectly valid response that simply ignored the parameter.
    /// </para>
    /// </remarks>
    internal class SiebelUrlParameterFormatter : DefaultUrlParameterFormatter
    {
        /// <inheritdoc/>
        public override string? Format(object? value, ICustomAttributeProvider attributeProvider, Type type)
        {
            if (value is bool flag)
            {
                return flag ? "true" : "false";
            }

            return base.Format(value, attributeProvider, type);
        }
    }
}
