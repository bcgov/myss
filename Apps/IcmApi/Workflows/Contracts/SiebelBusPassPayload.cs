namespace Icm.Api.Workflows.Contracts
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The service-request-level payload of a bus pass workflow message.
    /// </summary>
    /// <remarks>
    /// The SR classification triple — <see cref="SRType"/>, <see cref="SRSubType"/> and
    /// <see cref="SRSubSubType"/> — is the caller's to send, and must travel as a set:
    /// MEASURED SIT2 2026-09-14, a submission without it stores an SR with no sub type
    /// and no prospect Purpose, and <c>SRSubSubType</c> sent without its parents fails
    /// the upsert (bounded hierarchical picklist, <c>SBL-EAI-04401</c>). <see cref="Memo"/>
    /// carries <c>New Application</c> for a First Nations new application only — it is
    /// what lets that request file without a contact match. <see cref="Status"/>,
    /// <see cref="SvcOff"/>, <see cref="Priority"/>, <see cref="CommMethod"/> and
    /// <see cref="ClientId"/> stay unset; the workflow fills the first four itself.
    /// </remarks>
    internal class SiebelBusPassPayload
    {
        /// <summary>Gets or sets <c>ClientId</c>.</summary>
        [JsonPropertyName("ClientId")]
        public string? ClientId { get; set; }

        /// <summary>Gets or sets <c>CommMethod</c>.</summary>
        [JsonPropertyName("CommMethod")]
        public string? CommMethod { get; set; }

        /// <summary>Gets or sets <c>ICMBusPassRequestType</c>.</summary>
        [JsonPropertyName("ICMBusPassRequestType")]
        public string? ICMBusPassRequestType { get; set; }

        /// <summary>Gets or sets <c>SRSubSubType</c>.</summary>
        [JsonPropertyName("SRSubSubType")]
        public string? SRSubSubType { get; set; }

        /// <summary>Gets or sets <c>SRSubType</c>.</summary>
        [JsonPropertyName("SRSubType")]
        public string? SRSubType { get; set; }

        /// <summary>Gets or sets <c>SRType</c>.</summary>
        [JsonPropertyName("SRType")]
        public string? SRType { get; set; }

        /// <summary>Gets or sets <c>Status</c>.</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>SRKey</c>.</summary>
        [JsonPropertyName("SRKey")]
        public string? SRKey { get; set; }

        /// <summary>Gets or sets <c>SvcOff</c> (the service office).</summary>
        [JsonPropertyName("SvcOff")]
        public string? SvcOff { get; set; }

        /// <summary>Gets or sets <c>Priority</c>.</summary>
        [JsonPropertyName("Priority")]
        public string? Priority { get; set; }

        /// <summary>Gets or sets <c>Memo</c>.</summary>
        [JsonPropertyName("Memo")]
        public string? Memo { get; set; }

        /// <summary>Gets or sets the applicant list.</summary>
        [JsonPropertyName("ListOfSRProspects")]
        public SiebelBusPassProspectList? ListOfSRProspects { get; set; }

        /// <summary>Gets or sets the attachment list.</summary>
        [JsonPropertyName("ListOfSRAttachments")]
        public SiebelBusPassAttachmentList? ListOfSRAttachments { get; set; }
    }
}
