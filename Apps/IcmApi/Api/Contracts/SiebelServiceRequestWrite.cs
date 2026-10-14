namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A Siebel ServiceRequest record as it is <b>written</b>: the body of a POST or PUT on
    /// <c>data/ServiceRequest/ServiceRequest</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The write vocabulary is the read vocabulary</b> — the gateway's names, not the
    /// describe document's. MEASURED on SIT2 on 2026-09-29: <c>Type</c> is understood and
    /// <c>SR Type</c> is silently ignored (the sub type's picklist then has no parent and
    /// answers <c>SBL-EAI-04401</c>; a type-only body says <c>SR Type</c> is required), and
    /// <c>Are Any Of The Family Members Indigenous</c> reaches the field Siebel itself calls
    /// <c>ICMCPU Aborginal</c>. Siebel's own names appear only in its error messages.
    /// </para>
    /// <para>
    /// <b>A create needs two things a plain record does not have.</b> An <see cref="Id"/>,
    /// which is the integration component's only user key — without one a POST is
    /// <c>SBL-EAI-04397</c> — and which Siebel replaces with the row id it allocates
    /// (<c>1-53MU3WL</c> sent, <c>1-53MU3X7</c> created); and a <see cref="ListOfSRProspects"/>
    /// container, even an empty one, without which the body is "Invalid input"
    /// (<c>SBL-EAI-50243</c>). <see cref="ServiceRequestMapper"/> supplies both on a create.
    /// </para>
    /// <para>
    /// Only the fields the document does not mark read-only. Declared in ASCII order of
    /// field name, which is the order they serialize in — except <see cref="Type"/>, which
    /// goes before the two sub types because their picklists are constrained by it and
    /// Siebel validates in body order. Every property is nullable and unset ones are
    /// omitted (<see cref="IcmRefitSettings.JsonOptions"/>), so a PUT that sets one field
    /// changes one field.
    /// </para>
    /// </remarks>
    internal class SiebelServiceRequestWrite
    {
        /// <summary>Gets or sets <c>Are Any Of The Family Members Indigenous</c> (max 255). A bounded picklist on write; <c>TBD</c>, which reads show, is not an accepted value (<c>SBL-DAT-00225</c>).</summary>
        [JsonPropertyName("Are Any Of The Family Members Indigenous")]
        public string? AreAnyOfTheFamilyMembersIndigenous { get; set; }

        /// <summary>Gets or sets <c>Call Date</c> (<c>DTYPE_DATETIME</c>, written <c>MM/dd/yyyy HH:mm:ss</c>).</summary>
        [JsonPropertyName("Call Date")]
        public string? CallDate { get; set; }

        /// <summary>Gets or sets <c>Caller Address</c> (max 250).</summary>
        [JsonPropertyName("Caller Address")]
        public string? CallerAddress { get; set; }

        /// <summary>Gets or sets <c>Caller Email</c> (max 350).</summary>
        [JsonPropertyName("Caller Email")]
        public string? CallerEmail { get; set; }

        /// <summary>Gets or sets <c>Caller Name</c> (max 150).</summary>
        [JsonPropertyName("Caller Name")]
        public string? CallerName { get; set; }

        /// <summary>Gets or sets <c>Caller Phone</c> (max 40).</summary>
        [JsonPropertyName("Caller Phone")]
        public string? CallerPhone { get; set; }

        /// <summary>Gets or sets <c>Cell Phone</c> (max 40).</summary>
        [JsonPropertyName("Cell Phone")]
        public string? CellPhone { get; set; }

        /// <summary>Gets or sets <c>Comm Method</c> (max 30). Required by ICM (<c>SBL-DAT-00498</c>, MEASURED 2026-09-29).</summary>
        [JsonPropertyName("Comm Method")]
        public string? CommMethod { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c> (max 255).</summary>
        [JsonPropertyName("ICM BCSC DID")]
        public string? ICMBCSCDID { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Application Received Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("ICM CGA Application Received Flag")]
        public string? ICMCGAApplicationReceivedFlag { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Due Diligence Decision</c> (max 50).</summary>
        [JsonPropertyName("ICM CGA Due Diligence Decision")]
        public string? ICMCGADueDiligenceDecision { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Resolution Decision Date</c> (<c>DTYPE_DATE</c>, written <c>MM/dd/yyyy</c>).</summary>
        [JsonPropertyName("ICM CGA Resolution Decision Date")]
        public string? ICMCGAResolutionDecisionDate { get; set; }

        /// <summary>Gets or sets <c>ICM Stage</c> (max 50).</summary>
        [JsonPropertyName("ICM Stage")]
        public string? ICMStage { get; set; }

        /// <summary>
        /// Gets or sets <c>Id</c>. On an update, the key. On a create, the integration
        /// component's user key: any valid-looking value, which Siebel replaces with the
        /// row id it allocates (MEASURED 2026-09-29). A POST naming an <i>existing</i> id is
        /// refused as a duplicate (<c>SBL-EAI-04383</c>).
        /// </summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Integration Id</c> (max 30). Not a user key (MEASURED 2026-09-29).</summary>
        [JsonPropertyName("Integration Id")]
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets <c>Kkcfs</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Kkcfs")]
        public string? Kkcfs { get; set; }

        /// <summary>Gets or sets <c>Last Name</c> (max 50).</summary>
        [JsonPropertyName("Last Name")]
        public string? LastName { get; set; }

        /// <summary>
        /// Gets or sets <c>ListOfSRProspects</c>: the applicant rows nested Siebel-style — a
        /// container named for the list holding an array named for the component, the
        /// shape the bus pass workflow's own envelope uses. A bare <c>SRProspects</c> array
        /// is parsed as more service requests instead. The container must be present on a
        /// create, empty or not; rows inside it are MEASURED to be refused
        /// (<c>SBL-DAT-00276</c>), so a prospect is created afterwards by
        /// <see cref="IProspectApi.UpsertAsync"/>.
        /// </summary>
        [JsonPropertyName("ListOfSRProspects")]
        public SiebelProspectList? ListOfSRProspects { get; set; }

        /// <summary>Gets or sets <c>Memo</c> (max 255).</summary>
        [JsonPropertyName("Memo")]
        public string? Memo { get; set; }

        /// <summary>Gets or sets <c>Method</c> (max 30).</summary>
        [JsonPropertyName("Method")]
        public string? Method { get; set; }

        /// <summary>Gets or sets <c>Nature Of Call</c> (max 30).</summary>
        [JsonPropertyName("Nature Of Call")]
        public string? NatureOfCall { get; set; }

        /// <summary>Gets or sets <c>Pcc Summary</c> (max 2000).</summary>
        [JsonPropertyName("Pcc Summary")]
        public string? PccSummary { get; set; }

        /// <summary>Gets or sets <c>Preferred Contact Method</c> (max 30).</summary>
        [JsonPropertyName("Preferred Contact Method")]
        public string? PreferredContactMethod { get; set; }

        /// <summary>Gets or sets <c>Primary Contact Id</c> (max 15).</summary>
        [JsonPropertyName("Primary Contact Id")]
        public string? PrimaryContactId { get; set; }

        /// <summary>Gets or sets <c>Primary Organization Id</c> (max 15). Accepted, echoed by the write, and then not what the record holds: the created SR read back with the trusted user's default organization (MEASURED 2026-09-29, <c>1-5ZF8</c> sent, <c>0-R9NH</c> stored).</summary>
        [JsonPropertyName("Primary Organization Id")]
        public string? PrimaryOrganizationId { get; set; }

        /// <summary>Gets or sets <c>Priority</c> (max 30).</summary>
        [JsonPropertyName("Priority")]
        public string? Priority { get; set; }

        /// <summary>Gets or sets <c>Resolution</c> (max 3500).</summary>
        [JsonPropertyName("Resolution")]
        public string? Resolution { get; set; }

        /// <summary>Gets or sets <c>Restricted Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Restricted Flag")]
        public string? RestrictedFlag { get; set; }

        /// <summary>
        /// Gets or sets <c>Type</c> (max 30). Declared before the two sub types, out of ASCII
        /// order: their picklists are constrained by it and Siebel validates in body order.
        /// </summary>
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        /// <summary>Gets or sets <c>SR Sub Type</c> (max 30). After <see cref="Type"/>, for the reason given there.</summary>
        [JsonPropertyName("SR Sub Type")]
        public string? SRSubType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Sub Type</c> (max 30). After <see cref="SRSubType"/>.</summary>
        [JsonPropertyName("SR Sub Sub Type")]
        public string? SRSubSubType { get; set; }

        /// <summary>Gets or sets <c>Service Office</c> (max 100).</summary>
        [JsonPropertyName("Service Office")]
        public string? ServiceOffice { get; set; }

        /// <summary>Gets or sets <c>Service Request Number</c> (max 64). Normally Siebel's to assign; not a user key (MEASURED 2026-09-29).</summary>
        [JsonPropertyName("Service Request Number")]
        public string? ServiceRequestNumber { get; set; }

        /// <summary>Gets or sets <c>Status</c> (max 30).</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Type Of Caller</c> (max 30).</summary>
        [JsonPropertyName("Type Of Caller")]
        public string? TypeOfCaller { get; set; }
    }

    /// <summary>
    /// The <c>ListOfSRProspects</c> container of a service request write: Siebel's
    /// list-of-component shape around the nested prospect rows.
    /// </summary>
    internal class SiebelProspectList
    {
        /// <summary>Gets or sets the rows. Null serializes the container empty, <c>{}</c>, which a create accepts.</summary>
        [JsonPropertyName("SRProspects")]
        public IList<SiebelProspect>? SRProspects { get; set; }
    }
}
