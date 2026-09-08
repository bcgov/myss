namespace Icm.Api.Workflows.Contracts
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One <c>SRAttachments</c> row.
    /// </summary>
    /// <remarks>
    /// The <c>FromAdobe</c> in the old SOAP operation name suggests the original channel
    /// attached a rendered form here. Whether the workflow tolerates the block being
    /// absent — the spec marks the array <c>minItems: 1</c> but the block itself
    /// optional — is an open question recorded in the README.
    /// </remarks>
    internal class SiebelBusPassAttachment
    {
        /// <summary>Gets or sets <c>AttKey</c>.</summary>
        [JsonPropertyName("AttKey")]
        public string? AttKey { get; set; }

        /// <summary>Gets or sets <c>AttName</c> (the file name).</summary>
        [JsonPropertyName("AttName")]
        public string? AttName { get; set; }

        /// <summary>Gets or sets <c>Base64Strng</c>, the content (<c>DTYPE_ATTACHMENT</c>).</summary>
        [JsonPropertyName("Base64Strng")]
        public string? Base64Strng { get; set; }

        /// <summary>Gets or sets <c>OrgXMLStrng</c>.</summary>
        [JsonPropertyName("OrgXMLStrng")]
        public string? OrgXMLStrng { get; set; }
    }
}
