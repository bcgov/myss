namespace Icm.Api.Workflows.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The request body of the <c>ICM Receive Bus Pass Online Request Wrapper WF</c>
    /// workflow, exactly as <c>docs/integration/BusPassWorkflow_OpenApi.json</c> describes
    /// it: one <c>SRInboundMessage</c> wrapping single-element arrays all the way down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The nesting is Siebel's serialized integration-object hierarchy, not a design —
    /// <c>ListOfX</c> is an object holding an array named <c>X</c>, and the spec pins
    /// <c>ICMSRInbound</c>, <c>Header</c> and <c>Payload</c> to exactly one element each.
    /// It is mirrored rather than smoothed over; flattening it for callers is the
    /// mapper's job.
    /// </para>
    /// <para>
    /// <b>Untested.</b> No call has been made against a real ICM workflow endpoint. Unlike
    /// the Service Request contracts, whose field names were corrected against live
    /// responses, these names come only from the OpenAPI document — and that document
    /// describes the direct Siebel host, which for Service Requests disagreed with the API
    /// gateway on more than half the fields. The first live call should be captured raw.
    /// </para>
    /// </remarks>
    internal class SiebelBusPassEnvelope
    {
        /// <summary>Gets or sets the one message the envelope carries.</summary>
        [JsonPropertyName("SRInboundMessage")]
        public SiebelBusPassMessage? SRInboundMessage { get; set; }
    }

    /// <summary>
    /// The <c>SRInboundMessage</c>: fixed integration-object identification plus the
    /// record list.
    /// </summary>
    internal class SiebelBusPassMessage
    {
        /// <summary>Gets or sets <c>MessageId</c> (the spec's example is empty).</summary>
        [JsonPropertyName("MessageId")]
        public string? MessageId { get; set; }

        /// <summary>Gets or sets <c>MessageType</c> — always <c>Integration Object</c>.</summary>
        [JsonPropertyName("MessageType")]
        public string? MessageType { get; set; }

        /// <summary>Gets or sets <c>IntObjectName</c> — always <c>ICMSRBusPassInboundIO</c>.</summary>
        [JsonPropertyName("IntObjectName")]
        public string? IntObjectName { get; set; }

        /// <summary>Gets or sets <c>IntObjectFormat</c> — always <c>Siebel Hierarchical</c>.</summary>
        [JsonPropertyName("IntObjectFormat")]
        public string? IntObjectFormat { get; set; }

        /// <summary>Gets or sets the integration-object record list.</summary>
        [JsonPropertyName("ListOfICMSRBusPassInboundIO")]
        public SiebelBusPassInboundList? ListOfICMSRBusPassInboundIO { get; set; }
    }

    /// <summary>The <c>ListOfICMSRBusPassInboundIO</c> wrapper.</summary>
    internal class SiebelBusPassInboundList
    {
        /// <summary>Gets or sets <c>ICMSRInbound</c> — exactly one element per the spec.</summary>
        [JsonPropertyName("ICMSRInbound")]
        public IList<SiebelBusPassInbound>? ICMSRInbound { get; set; }
    }

    /// <summary>One inbound record: a header and a payload, each a one-element list.</summary>
    internal class SiebelBusPassInbound
    {
        /// <summary>Gets or sets the header list.</summary>
        [JsonPropertyName("ListOfHeader")]
        public SiebelBusPassHeaderList? ListOfHeader { get; set; }

        /// <summary>Gets or sets the payload list.</summary>
        [JsonPropertyName("ListOfPayload")]
        public SiebelBusPassPayloadList? ListOfPayload { get; set; }
    }

    /// <summary>The <c>ListOfHeader</c> wrapper.</summary>
    internal class SiebelBusPassHeaderList
    {
        /// <summary>Gets or sets <c>Header</c> — exactly one element per the spec.</summary>
        [JsonPropertyName("Header")]
        public IList<SiebelBusPassHeader>? Header { get; set; }
    }

    /// <summary>The <c>ListOfPayload</c> wrapper.</summary>
    internal class SiebelBusPassPayloadList
    {
        /// <summary>Gets or sets <c>Payload</c> — exactly one element per the spec.</summary>
        [JsonPropertyName("Payload")]
        public IList<SiebelBusPassPayload>? Payload { get; set; }
    }

    /// <summary>The <c>ListOfSRProspects</c> wrapper.</summary>
    internal class SiebelBusPassProspectList
    {
        /// <summary>Gets or sets <c>SRProspects</c> (at least one element per the spec).</summary>
        [JsonPropertyName("SRProspects")]
        public IList<SiebelBusPassProspect>? SRProspects { get; set; }
    }

    /// <summary>The <c>ListOfSRAttachments</c> wrapper.</summary>
    internal class SiebelBusPassAttachmentList
    {
        /// <summary>Gets or sets <c>SRAttachments</c>.</summary>
        [JsonPropertyName("SRAttachments")]
        public IList<SiebelBusPassAttachment>? SRAttachments { get; set; }
    }
}
