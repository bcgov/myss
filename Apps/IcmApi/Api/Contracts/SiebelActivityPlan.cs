namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A Siebel <c>ActivityPlan/ActivityPlan</c> record as it appears on the wire — a plan
    /// <i>instance</i> applied to a service request, whose steps are
    /// <see cref="SiebelActivity"/> rows (<c>docs/integration/ActivityPlan_OpenApi.json</c>).
    /// </summary>
    /// <remarks>
    /// The document's 33 names are the live names (MEASURED SIT2 2026-09-29, standalone
    /// and as a service request's <c>ActivityPlan</c> child). <see cref="Template"/> names
    /// the plan template — <c>Bus Pass - New Intake</c>, template id <c>1-30LDVGD</c> — and
    /// <see cref="Status"/> runs <c>In Progress</c> → <c>Closed</c> or <c>Cancelled</c>.
    /// Read only: a PUT with <c>"Id": "NULL"</c> is refused because the write-side
    /// <c>Template</c> picklist does not contain the bus pass template
    /// (<c>SBL-EAI-04401</c>, with or without <c>Template Id</c>, MEASURED 2026-09-29).
    /// </remarks>
    internal class SiebelActivityPlan
    {
        /// <summary>Gets or sets <c>Id</c>. The row id, which a step's <c>Activity Template Id</c> points at.</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Activity SR Id</c>. The service request the plan is applied to (max 15).</summary>
        [JsonPropertyName("Activity SR Id")]
        public string? ActivitySRId { get; set; }

        /// <summary>Gets or sets <c>Activity UID</c> (max 30). Equals the row id when seen.</summary>
        [JsonPropertyName("Activity UID")]
        public string? ActivityUID { get; set; }

        /// <summary>Gets or sets <c>SR Number</c>; read-only.</summary>
        [JsonPropertyName("SR Number")]
        public string? SRNumber { get; set; }

        /// <summary>Gets or sets <c>SR Type</c>; read-only.</summary>
        [JsonPropertyName("SR Type")]
        public string? SRType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Type</c>; read-only.</summary>
        [JsonPropertyName("SR Sub Type")]
        public string? SRSubType { get; set; }

        /// <summary>Gets or sets <c>Case Id</c> (max 15).</summary>
        [JsonPropertyName("Case Id")]
        public string? CaseId { get; set; }

        /// <summary>Gets or sets <c>Case #</c>; read-only.</summary>
        [JsonPropertyName("Case #")]
        public string? CaseNumber { get; set; }

        /// <summary>Gets or sets <c>Case Type</c>; read-only.</summary>
        [JsonPropertyName("Case Type")]
        public string? CaseType { get; set; }

        /// <summary>Gets or sets <c>Incident Id</c> (max 15).</summary>
        [JsonPropertyName("Incident Id")]
        public string? IncidentId { get; set; }

        /// <summary>Gets or sets <c>Incident #</c>; read-only.</summary>
        [JsonPropertyName("Incident #")]
        public string? IncidentNumber { get; set; }

        /// <summary>Gets or sets <c>Template</c>, the plan template's name (max 100).</summary>
        [JsonPropertyName("Template")]
        public string? Template { get; set; }

        /// <summary>Gets or sets <c>Template Id</c>, the plan template's row id (max 15).</summary>
        [JsonPropertyName("Template Id")]
        public string? TemplateId { get; set; }

        /// <summary>Gets or sets <c>Template Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Template Flag")]
        public string? TemplateFlag { get; set; }

        /// <summary>Gets or sets <c>Status</c> (max 30).</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Comments</c> (max 1500).</summary>
        [JsonPropertyName("Comments")]
        public string? Comments { get; set; }

        /// <summary>Gets or sets <c>Completed Date</c> (<c>DTYPE_UTCDATETIME</c>).</summary>
        [JsonPropertyName("Completed Date")]
        public string? CompletedDate { get; set; }

        /// <summary>Gets or sets <c>Restricted Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Restricted Flag")]
        public string? RestrictedFlag { get; set; }

        /// <summary>Gets or sets <c>Shared Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Shared Flag")]
        public string? SharedFlag { get; set; }

        /// <summary>Gets or sets <c>ICM Created By Office</c> (max 100).</summary>
        [JsonPropertyName("ICM Created By Office")]
        public string? ICMCreatedByOffice { get; set; }

        /// <summary>Gets or sets <c>ICM Created By Office Id</c> (max 15).</summary>
        [JsonPropertyName("ICM Created By Office Id")]
        public string? ICMCreatedByOfficeId { get; set; }

        /// <summary>Gets or sets <c>Created By Login</c>; read-only.</summary>
        [JsonPropertyName("Created By Login")]
        public string? CreatedByLogin { get; set; }

        /// <summary>Gets or sets <c>Created By Name</c>; read-only.</summary>
        [JsonPropertyName("Created By Name")]
        public string? CreatedByName { get; set; }

        /// <summary>Gets or sets the record's links. Declared so they do not land in <see cref="AdditionalFields"/>. Not mapped.</summary>
        [JsonPropertyName("Link")]
        [JsonConverter(typeof(SiebelLinkListConverter))]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets or sets every field ICM sent that is not declared above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
