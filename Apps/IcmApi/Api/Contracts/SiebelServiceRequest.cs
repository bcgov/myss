namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A Siebel ServiceRequest record exactly as it appears on the wire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The field names here come from real responses, not from the OpenAPI document.</b>
    /// MEASURED against SIT on 2026-08-28 over 100 records: the two disagree on 27 of the
    /// 51 fields. The spec calls the SR number <c>SR Number</c>; ICM sends
    /// <c>Service Request Number</c>. The spec says <c>SR Type</c>; ICM sends <c>Type</c>.
    /// A name taken from the spec compiles, returns 200, and yields null for ever — which
    /// is how this went unnoticed until a raw response was read.
    /// </para>
    /// <para>
    /// Not an environment difference: the SIT1 and SIT2 documents are the same document
    /// bar one field, and both disagree with the live endpoint equally. Both describe the
    /// direct Siebel host on port 8443, while this client calls the API gateway — see the
    /// README for what is known and what is still inference.
    /// </para>
    /// <para>
    /// The id/name pairs are the subtle ones. The spec's <c>Created By</c> is a row id,
    /// which ICM calls <c>Created By Id</c>; ICM's <c>Created By</c> is the login name the
    /// spec calls <c>Created By Name</c>. Same for the Updated pair.
    /// </para>
    /// <para>
    /// <see cref="AdditionalFields"/> catches anything not listed below, so the next
    /// disagreement surfaces as data rather than as silence.
    /// </para>
    /// </remarks>
    internal class SiebelServiceRequest
    {
        /// <summary>Gets or sets <c>Address</c> (max 255; read-only).</summary>
        [JsonPropertyName("Address")]
        public string? Address { get; set; }

        /// <summary>Gets or sets <c>Address Comments</c> (max 255; read-only; never seen with a value).</summary>
        [JsonPropertyName("Address Comments")]
        public string? AddressComments { get; set; }

        /// <summary>Gets or sets <c>Are Any Of The Family Members Indigenous</c> (max 255).</summary>
        [JsonPropertyName("Are Any Of The Family Members Indigenous")]
        public string? AreAnyOfTheFamilyMembersIndigenous { get; set; }

        /// <summary>Gets or sets <c>Assigned To</c> (max 255; read-only).</summary>
        [JsonPropertyName("Assigned To")]
        public string? AssignedTo { get; set; }

        /// <summary>Gets or sets <c>Assigned To Id</c> (max 15; read-only).</summary>
        [JsonPropertyName("Assigned To Id")]
        public string? AssignedToId { get; set; }

        /// <summary>Gets or sets <c>Call Date</c> (a date and time, with no time zone).</summary>
        [JsonPropertyName("Call Date")]
        public string? CallDate { get; set; }

        /// <summary>Gets or sets <c>Caller Address</c> (max 250; never seen with a value).</summary>
        [JsonPropertyName("Caller Address")]
        public string? CallerAddress { get; set; }

        /// <summary>Gets or sets <c>Caller Email</c> (max 350; never seen with a value).</summary>
        [JsonPropertyName("Caller Email")]
        public string? CallerEmail { get; set; }

        /// <summary>Gets or sets <c>Caller Name</c> (max 150; never seen with a value).</summary>
        [JsonPropertyName("Caller Name")]
        public string? CallerName { get; set; }

        /// <summary>Gets or sets <c>Caller Phone</c> (max 40; never seen with a value).</summary>
        [JsonPropertyName("Caller Phone")]
        public string? CallerPhone { get; set; }

        /// <summary>Gets or sets <c>Case Local Office</c> (max 255; read-only).</summary>
        [JsonPropertyName("Case Local Office")]
        public string? CaseLocalOffice { get; set; }

        /// <summary>Gets or sets <c>Cell Phone</c> (max 40).</summary>
        [JsonPropertyName("Cell Phone")]
        public string? CellPhone { get; set; }

        /// <summary>Gets or sets <c>Closed Date</c> (a date and time, with no time zone; read-only).</summary>
        [JsonPropertyName("Closed Date")]
        public string? ClosedDate { get; set; }

        /// <summary>Gets or sets <c>Comm Method</c> (max 30).</summary>
        [JsonPropertyName("Comm Method")]
        public string? CommMethod { get; set; }

        /// <summary>Gets or sets <c>Created By</c> (max 50; read-only).</summary>
        [JsonPropertyName("Created By")]
        public string? CreatedBy { get; set; }

        /// <summary>Gets or sets <c>Created By Id</c> (max 15; read-only).</summary>
        [JsonPropertyName("Created By Id")]
        public string? CreatedById { get; set; }

        /// <summary>Gets or sets <c>Created By Office</c> (max 100; read-only).</summary>
        [JsonPropertyName("Created By Office")]
        public string? CreatedByOffice { get; set; }

        /// <summary>Gets or sets <c>Created Date</c> (a date and time, with no time zone; read-only).</summary>
        [JsonPropertyName("Created Date")]
        public string? CreatedDate { get; set; }

        /// <summary>Gets or sets <c>Given Names</c> (max 255; read-only).</summary>
        [JsonPropertyName("Given Names")]
        public string? GivenNames { get; set; }

        /// <summary>Gets or sets <c>Home Phone</c> (max 40; read-only; never seen with a value).</summary>
        [JsonPropertyName("Home Phone")]
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c> (max 255; never seen with a value).</summary>
        [JsonPropertyName("ICM BCSC DID")]
        public string? ICMBCSCDID { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Application Received Flag</c> (a Y/N flag; never seen with a value).</summary>
        [JsonPropertyName("ICM CGA Application Received Flag")]
        public string? ICMCGAApplicationReceivedFlag { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Due Diligence Decision</c> (max 50; never seen with a value).</summary>
        [JsonPropertyName("ICM CGA Due Diligence Decision")]
        public string? ICMCGADueDiligenceDecision { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Resolution Decision Date</c> (a date; never seen with a value).</summary>
        [JsonPropertyName("ICM CGA Resolution Decision Date")]
        public string? ICMCGAResolutionDecisionDate { get; set; }

        /// <summary>Gets or sets <c>ICM Stage</c> (max 50; never seen with a value).</summary>
        [JsonPropertyName("ICM Stage")]
        public string? ICMStage { get; set; }

        /// <summary>Gets or sets <c>Id</c> (read-only; not in the OpenAPI document).</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Integration Id</c> (max 30; never seen with a value).</summary>
        [JsonPropertyName("Integration Id")]
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets <c>Kkcfs</c> (a Y/N flag).</summary>
        [JsonPropertyName("Kkcfs")]
        public string? Kkcfs { get; set; }

        /// <summary>Gets or sets <c>Last Name</c> (max 50).</summary>
        [JsonPropertyName("Last Name")]
        public string? LastName { get; set; }

        /// <summary>Gets or sets <c>Memo</c> (max 255; never seen with a value).</summary>
        [JsonPropertyName("Memo")]
        public string? Memo { get; set; }

        /// <summary>Gets or sets <c>Method</c> (max 30; never seen with a value).</summary>
        [JsonPropertyName("Method")]
        public string? Method { get; set; }

        /// <summary>Gets or sets <c>Nature Of Call</c> (max 30; never seen with a value).</summary>
        [JsonPropertyName("Nature Of Call")]
        public string? NatureOfCall { get; set; }

        /// <summary>Gets or sets <c>Pcc Summary</c> (max 2000; never seen with a value).</summary>
        [JsonPropertyName("Pcc Summary")]
        public string? PccSummary { get; set; }

        /// <summary>Gets or sets <c>Preferred Contact Method</c> (max 30; never seen with a value).</summary>
        [JsonPropertyName("Preferred Contact Method")]
        public string? PreferredContactMethod { get; set; }

        /// <summary>Gets or sets <c>Primary Contact Id</c> (max 15).</summary>
        [JsonPropertyName("Primary Contact Id")]
        public string? PrimaryContactId { get; set; }

        /// <summary>Gets or sets <c>Primary Organization Id</c> (max 15).</summary>
        [JsonPropertyName("Primary Organization Id")]
        public string? PrimaryOrganizationId { get; set; }

        /// <summary>Gets or sets <c>Primary Organization Name</c> (max 100; read-only).</summary>
        [JsonPropertyName("Primary Organization Name")]
        public string? PrimaryOrganizationName { get; set; }

        /// <summary>Gets or sets <c>Priority</c> (max 30).</summary>
        [JsonPropertyName("Priority")]
        public string? Priority { get; set; }

        /// <summary>Gets or sets <c>Resolution</c> (max 3500).</summary>
        [JsonPropertyName("Resolution")]
        public string? Resolution { get; set; }

        /// <summary>Gets or sets <c>Restricted Flag</c> (a Y/N flag).</summary>
        [JsonPropertyName("Restricted Flag")]
        public string? RestrictedFlag { get; set; }

        /// <summary>Gets or sets <c>Row Id</c> (read-only; not in the OpenAPI document).</summary>
        [JsonPropertyName("Row Id")]
        public string? RowId { get; set; }

        /// <summary>Gets or sets <c>SR Sub Sub Type</c> (max 30).</summary>
        [JsonPropertyName("SR Sub Sub Type")]
        public string? SRSubSubType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Type</c> (max 30).</summary>
        [JsonPropertyName("SR Sub Type")]
        public string? SRSubType { get; set; }

        /// <summary>Gets or sets <c>Service Office</c> (max 100).</summary>
        [JsonPropertyName("Service Office")]
        public string? ServiceOffice { get; set; }

        /// <summary>Gets or sets <c>Service Request Number</c> (max 64).</summary>
        [JsonPropertyName("Service Request Number")]
        public string? ServiceRequestNumber { get; set; }

        /// <summary>Gets or sets <c>Status</c> (max 30).</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Type</c> (max 30).</summary>
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        /// <summary>Gets or sets <c>Type Of Caller</c> (max 30; never seen with a value).</summary>
        [JsonPropertyName("Type Of Caller")]
        public string? TypeOfCaller { get; set; }

        /// <summary>Gets or sets <c>Updated By</c> (max 50; read-only).</summary>
        [JsonPropertyName("Updated By")]
        public string? UpdatedBy { get; set; }

        /// <summary>Gets or sets <c>Updated By Id</c> (max 15; read-only).</summary>
        [JsonPropertyName("Updated By Id")]
        public string? UpdatedById { get; set; }

        /// <summary>Gets or sets <c>Updated Date</c> (a date and time, with no time zone; read-only).</summary>
        [JsonPropertyName("Updated Date")]
        public string? UpdatedDate { get; set; }

        /// <summary>Gets or sets <c>Link</c>, the self and child links.</summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>
        /// Gets or sets every field in the response that has no property above, as the
        /// raw JSON that arrived.
        /// </summary>
        /// <remarks>
        /// Without this, an unrecognised field is discarded by the deserializer without a
        /// trace. A record from a business component this client has not seen, or a field
        /// ICM adds later, now arrives intact and can be read rather than guessed at.
        /// Nothing here is ever sent back: a write builds a fresh instance.
        /// </remarks>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
