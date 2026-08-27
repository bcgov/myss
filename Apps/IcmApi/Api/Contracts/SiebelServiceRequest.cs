namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A Siebel ServiceRequest record exactly as it appears on the wire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Internal on purpose. This is Siebel's shape, not MySS's: every field is a
    /// nullable string whatever it actually holds, names carry spaces and punctuation,
    /// and read-only fields sit beside writable ones with nothing to tell them apart.
    /// <see cref="Icm.Api.Models.ServiceRequest"/> is what leaves this assembly;
    /// <see cref="ServiceRequestMapper"/> is the only thing that converts between them.
    /// </para>
    /// <para>
    /// One class serves all three body schemas in the spec — the POST/PUT request
    /// (<c>data_ServiceRequest_ServiceRequest_</c>), the single GET response
    /// (adds <see cref="Link"/>) and the write response (adds <see cref="Id"/>) — because
    /// they differ only by those two fields.
    /// </para>
    /// <para>
    /// Nulls are not serialized (see <see cref="IcmRefitSettings"/>), which is what makes
    /// a partial write possible.
    /// </para>
    /// </remarks>
    internal class SiebelServiceRequest
    {
        /// <summary>Gets or sets <c>Address Comments</c> (text; max 255; read-only).</summary>
        [JsonPropertyName("Address Comments")]
        public string? AddressComments { get; set; }

        /// <summary>Gets or sets <c>ICMCPU Aborginal</c> (text; max 255).</summary>
        [JsonPropertyName("ICMCPU Aborginal")]
        public string? ICMCPUAborginal { get; set; }

        /// <summary>Gets or sets <c>Call Date</c> (date/time; max 32).</summary>
        [JsonPropertyName("Call Date")]
        public string? CallDate { get; set; }

        /// <summary>Gets or sets <c>CP Caller Address</c> (text; max 250).</summary>
        [JsonPropertyName("CP Caller Address")]
        public string? CPCallerAddress { get; set; }

        /// <summary>Gets or sets <c>CP Caller Email</c> (text; max 350).</summary>
        [JsonPropertyName("CP Caller Email")]
        public string? CPCallerEmail { get; set; }

        /// <summary>Gets or sets <c>CP Caller Name</c> (text; max 150).</summary>
        [JsonPropertyName("CP Caller Name")]
        public string? CPCallerName { get; set; }

        /// <summary>Gets or sets <c>CP Caller Phone</c> (phone; max 40).</summary>
        [JsonPropertyName("CP Caller Phone")]
        public string? CPCallerPhone { get; set; }

        /// <summary>Gets or sets <c>Contact Cell #</c> (phone; max 40).</summary>
        [JsonPropertyName("Contact Cell #")]
        public string? ContactCellNumber { get; set; }

        /// <summary>Gets or sets <c>ICM Created By Office</c> (text; max 100; read-only).</summary>
        [JsonPropertyName("ICM Created By Office")]
        public string? ICMCreatedByOffice { get; set; }

        /// <summary>Gets or sets <c>Contact Given Name</c> (text; max 255; read-only).</summary>
        [JsonPropertyName("Contact Given Name")]
        public string? ContactGivenName { get; set; }

        /// <summary>Gets or sets <c>Contact Home Phone</c> (phone; max 40; read-only).</summary>
        [JsonPropertyName("Contact Home Phone")]
        public string? ContactHomePhone { get; set; }

        /// <summary>Gets or sets <c>KKCFS Flag</c> (flag; max 1).</summary>
        [JsonPropertyName("KKCFS Flag")]
        public string? KKCFSFlag { get; set; }

        /// <summary>Gets or sets <c>Case Local Office</c> (text; max 255; read-only).</summary>
        [JsonPropertyName("Case Local Office")]
        public string? CaseLocalOffice { get; set; }

        /// <summary>Gets or sets <c>Memo</c> (text; max 255).</summary>
        [JsonPropertyName("Memo")]
        public string? Memo { get; set; }

        /// <summary>Gets or sets <c>CP Nature of Call</c> (text; max 30).</summary>
        [JsonPropertyName("CP Nature of Call")]
        public string? CPNatureOfCall { get; set; }

        /// <summary>Gets or sets <c>CP PCC Analysis</c> (text; max 2000).</summary>
        [JsonPropertyName("CP PCC Analysis")]
        public string? CPPCCAnalysis { get; set; }

        /// <summary>Gets or sets <c>CP Caller Pref Contact Method</c> (text; max 30).</summary>
        [JsonPropertyName("CP Caller Pref Contact Method")]
        public string? CPCallerPrefContactMethod { get; set; }

        /// <summary>Gets or sets <c>Restricted Flag</c> (flag; max 1).</summary>
        [JsonPropertyName("Restricted Flag")]
        public string? RestrictedFlag { get; set; }

        /// <summary>Gets or sets <c>CP Caller Type</c> (text; max 30).</summary>
        [JsonPropertyName("CP Caller Type")]
        public string? CPCallerType { get; set; }

        /// <summary>Gets or sets <c>Primary Contact Id</c> (Siebel row id; max 15).</summary>
        [JsonPropertyName("Primary Contact Id")]
        public string? PrimaryContactId { get; set; }

        /// <summary>Gets or sets <c>ICM Stage</c> (text; max 50).</summary>
        [JsonPropertyName("ICM Stage")]
        public string? ICMStage { get; set; }

        /// <summary>Gets or sets <c>Primary Organization Id</c> (Siebel row id; max 15).</summary>
        [JsonPropertyName("Primary Organization Id")]
        public string? PrimaryOrganizationId { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Due Diligence Decision</c> (text; max 50).</summary>
        [JsonPropertyName("ICM CGA Due Diligence Decision")]
        public string? ICMCGADueDiligenceDecision { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Resolution Decision Date</c> (date; max 32).</summary>
        [JsonPropertyName("ICM CGA Resolution Decision Date")]
        public string? ICMCGAResolutionDecisionDate { get; set; }

        /// <summary>Gets or sets <c>Primary Organization Name</c> (text; max 100; read-only).</summary>
        [JsonPropertyName("Primary Organization Name")]
        public string? PrimaryOrganizationName { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Application Received Flag</c> (flag; max 1).</summary>
        [JsonPropertyName("ICM CGA Application Received Flag")]
        public string? ICMCGAApplicationReceivedFlag { get; set; }

        /// <summary>Gets or sets <c>CP Outcome</c> (text; max 3500).</summary>
        [JsonPropertyName("CP Outcome")]
        public string? CPOutcome { get; set; }

        /// <summary>Gets or sets <c>Created</c> (UTC date/time; max 32; read-only; required on a complete record).</summary>
        [JsonPropertyName("Created")]
        public string? Created { get; set; }

        /// <summary>Gets or sets <c>Created By</c> (Siebel row id; max 15; read-only; required on a complete record).</summary>
        [JsonPropertyName("Created By")]
        public string? CreatedBy { get; set; }

        /// <summary>Gets or sets <c>Updated</c> (UTC date/time; max 32; read-only; required on a complete record).</summary>
        [JsonPropertyName("Updated")]
        public string? Updated { get; set; }

        /// <summary>Gets or sets <c>Updated By Name</c> (text; max 50; read-only).</summary>
        [JsonPropertyName("Updated By Name")]
        public string? UpdatedByName { get; set; }

        /// <summary>Gets or sets <c>Updated By</c> (Siebel row id; max 15; read-only; required on a complete record).</summary>
        [JsonPropertyName("Updated By")]
        public string? UpdatedBy { get; set; }

        /// <summary>Gets or sets <c>SR KP Address Calc</c> (text; max 255; read-only).</summary>
        [JsonPropertyName("SR KP Address Calc")]
        public string? SRKPAddressCalc { get; set; }

        /// <summary>Gets or sets <c>Close Date Calc</c> (UTC date/time; max 32; read-only).</summary>
        [JsonPropertyName("Close Date Calc")]
        public string? CloseDateCalc { get; set; }

        /// <summary>Gets or sets <c>Comm Method</c> (text; max 30; required on a complete record).</summary>
        [JsonPropertyName("Comm Method")]
        public string? CommMethod { get; set; }

        /// <summary>Gets or sets <c>Contact Last Name</c> (text; max 50).</summary>
        [JsonPropertyName("Contact Last Name")]
        public string? ContactLastName { get; set; }

        /// <summary>Gets or sets <c>Created By Name</c> (text; max 50; read-only).</summary>
        [JsonPropertyName("Created By Name")]
        public string? CreatedByName { get; set; }

        /// <summary>Gets or sets <c>Integration Id</c> (text; max 30).</summary>
        [JsonPropertyName("Integration Id")]
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets <c>CP Caller Method</c> (text; max 30).</summary>
        [JsonPropertyName("CP Caller Method")]
        public string? CPCallerMethod { get; set; }

        /// <summary>Gets or sets <c>Assigned To Id</c> (Siebel row id; max 15; read-only).</summary>
        [JsonPropertyName("Assigned To Id")]
        public string? AssignedToId { get; set; }

        /// <summary>Gets or sets <c>Assigned To</c> (text; max 255; read-only).</summary>
        [JsonPropertyName("Assigned To")]
        public string? AssignedTo { get; set; }

        /// <summary>Gets or sets <c>Priority</c> (text; max 30; required on a complete record).</summary>
        [JsonPropertyName("Priority")]
        public string? Priority { get; set; }

        /// <summary>Gets or sets <c>Resolution Code</c> (text; max 30).</summary>
        [JsonPropertyName("Resolution Code")]
        public string? ResolutionCode { get; set; }

        /// <summary>Gets or sets <c>SR Number</c> (text; max 64).</summary>
        [JsonPropertyName("SR Number")]
        public string? SRNumber { get; set; }

        /// <summary>Gets or sets <c>SR Type</c> (text; max 30; required on a complete record).</summary>
        [JsonPropertyName("SR Type")]
        public string? SRType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Type</c> (text; max 30).</summary>
        [JsonPropertyName("SR Sub Type")]
        public string? SRSubType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Sub Type</c> (text; max 30).</summary>
        [JsonPropertyName("SR Sub Sub Type")]
        public string? SRSubSubType { get; set; }

        /// <summary>Gets or sets <c>Status</c> (text; max 30; required on a complete record).</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Service Office</c> (text; max 100; required on a complete record).</summary>
        [JsonPropertyName("Service Office")]
        public string? ServiceOffice { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c> (text; max 255).</summary>
        [JsonPropertyName("ICM BCSC DID")]
        public string? ICMBCSCDID { get; set; }

        /// <summary>Gets or sets <c>Id</c>, the Siebel row id. Returned on a write response.</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Link</c>, the child links, when the request asked for them.</summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }
    }
}
