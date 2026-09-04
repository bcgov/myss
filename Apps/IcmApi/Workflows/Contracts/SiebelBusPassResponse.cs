namespace Icm.Api.Workflows.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The workflow's out-args: an application number and an outcome, with the
    /// spaced field names the OpenAPI document declares.
    /// </summary>
    /// <remarks>
    /// Unlike the request, half these names carry spaces (<c>Error Code</c>) and half do
    /// not (<c>ApplicationNumber</c>) — that inconsistency is the document's, kept
    /// verbatim. A business failure is expected to arrive as a <c>200</c> with
    /// <c>Error Code</c> populated, not as an HTTP error; nothing has confirmed that
    /// against a live workflow yet.
    /// </remarks>
    internal class SiebelBusPassResponse
    {
        /// <summary>Gets or sets <c>ApplicationNumber</c>.</summary>
        [JsonPropertyName("ApplicationNumber")]
        public string? ApplicationNumber { get; set; }

        /// <summary>Gets or sets <c>Error Code</c>.</summary>
        [JsonPropertyName("Error Code")]
        public string? ErrorCode { get; set; }

        /// <summary>Gets or sets <c>Error Message</c>.</summary>
        [JsonPropertyName("Error Message")]
        public string? ErrorMessage { get; set; }

        /// <summary>Gets or sets <c>First Name</c>.</summary>
        [JsonPropertyName("First Name")]
        public string? FirstName { get; set; }

        /// <summary>Gets or sets <c>Last Name</c>.</summary>
        [JsonPropertyName("Last Name")]
        public string? LastName { get; set; }

        /// <summary>Gets or sets <c>Status</c>.</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets every field in the response that has no property above, as the raw
        /// JSON that arrived.
        /// </summary>
        /// <remarks>
        /// The Service Request spec disagreed with the live gateway on 27 of 51 field
        /// names, and the mismatch was invisible until unmodelled fields were kept. Same
        /// insurance here: if the live workflow answers with different names, they land in
        /// this dictionary as data rather than vanishing.
        /// </remarks>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
