namespace Icm.Api.Contracts
{
    using System.Text.Json.Serialization;

    /// <summary>A Siebel HATEOAS link as it appears on the wire.</summary>
    internal class SiebelLink
    {
        /// <summary>Gets or sets the relation, e.g. <c>self</c> or a child business component.</summary>
        [JsonPropertyName("rel")]
        public string? Rel { get; set; }

        /// <summary>
        /// Gets or sets the target address. A string rather than a <see cref="System.Uri"/>
        /// because Siebel is free to return a relative or malformed value, and a wire model
        /// should record what arrived rather than fail to deserialize it.
        /// </summary>
        [JsonPropertyName("href")]
        public string? Href { get; set; }

        /// <summary>Gets or sets the link name.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
