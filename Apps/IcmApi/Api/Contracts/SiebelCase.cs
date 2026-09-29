namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>A Siebel <c>Cases/Case</c> record as it appears on the wire — the fields this library asks for, not the 52 the business component declares.</summary>
    /// <remarks>
    /// <para>
    /// <b>Names are the live ones.</b> The business component behaves like the service
    /// request, not like the contact: MEASURED on SIT2 on 2026-09-24, ICM sends <c>Id</c>,
    /// <c>Row Id</c>, <c>Assigned To</c>, <c>Created Date</c>, <c>Updated Date</c>,
    /// <c>Created By Id</c> and <c>Updated By Id</c>, none of which
    /// <c>docs/integration/Case_OpenApi.json</c> declares, and its <c>Created By</c> is a login
    /// where the document's is a row id. The document's <c>Created</c>, <c>Sales Rep</c>,
    /// <c>ICM Created By</c> and <c>ICM Updated By</c> are rejected in a <c>fields</c> list
    /// (<c>SBL-EAI-50258</c>), so they are not asked for. Every other name below was accepted.
    /// </para>
    /// <para>
    /// Everything is a nullable string because that is what Siebel sends; typing it is
    /// <see cref="CaseMapper"/>'s job. <see cref="AdditionalFields"/> catches anything not
    /// listed, so a renamed field surfaces as data rather than as a property that is null
    /// for ever.
    /// </para>
    /// </remarks>
    internal class SiebelCase
    {
        /// <summary>Gets or sets <c>Id</c>. The row id, the <c>case_key</c> every other call takes. Not in the document; always sent.</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Case Num</c>. The case number ICM's screens show (max 100).</summary>
        [JsonPropertyName("Case Num")]
        public string? CaseNum { get; set; }

        /// <summary>Gets or sets <c>Name</c>. The case name — <c>LAST, FIRST</c> of the key player when seen (max 100).</summary>
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        /// <summary>Gets or sets <c>Type</c>. The program, e.g. <c>Employment and Assistance</c> (max 30).</summary>
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        /// <summary>Gets or sets <c>Status</c>. <c>Open</c> or <c>Closed</c> when seen (max 30).</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Close Reason</c>. Why it was closed (max 30).</summary>
        [JsonPropertyName("Close Reason")]
        public string? CloseReason { get; set; }

        /// <summary>Gets or sets <c>Closed Date</c> (a date; <c>MM/DD/YYYY</c> when seen). When it was closed.</summary>
        [JsonPropertyName("Closed Date")]
        public string? ClosedDate { get; set; }

        /// <summary>Gets or sets <c>Early Open Reason</c>. (max 30).</summary>
        [JsonPropertyName("Early Open Reason")]
        public string? EarlyOpenReason { get; set; }

        /// <summary>Gets or sets <c>Reopened Date</c> (a date; <c>MM/DD/YYYY</c> when seen). When it was last reopened.</summary>
        [JsonPropertyName("Reopened Date")]
        public string? ReopenedDate { get; set; }

        /// <summary>Gets or sets <c>Renew Review Date</c> (a date; <c>MM/DD/YYYY</c> when seen). When the case is next reviewed.</summary>
        [JsonPropertyName("Renew Review Date")]
        public string? RenewReviewDate { get; set; }

        /// <summary>Gets or sets <c>Caseload</c>. The caseload code (max 3).</summary>
        [JsonPropertyName("Caseload")]
        public string? Caseload { get; set; }

        /// <summary>Gets or sets <c>Work Queue</c>. E.g. <c>SSA-Self Serve Application</c> (max 30).</summary>
        [JsonPropertyName("Work Queue")]
        public string? WorkQueue { get; set; }

        /// <summary>Gets or sets <c>Office Name</c>. The ministry office, e.g. <c>106 - Victoria Vefra</c> (max 100).</summary>
        [JsonPropertyName("Office Name")]
        public string? OfficeName { get; set; }

        /// <summary>Gets or sets <c>Region Name</c>. The service region, e.g. <c>MHSD - Vancouver Island</c>; read-only.</summary>
        [JsonPropertyName("Region Name")]
        public string? RegionName { get; set; }

        /// <summary>Gets or sets <c>Organization</c>. The owning organization, e.g. <c>MHSD</c>; read-only.</summary>
        [JsonPropertyName("Organization")]
        public string? Organization { get; set; }

        /// <summary>Gets or sets <c>Legacy File Number</c>. The pre-ICM file number, e.g. <c>GA04046116</c> (max 15).</summary>
        [JsonPropertyName("Legacy File Number")]
        public string? LegacyFileNumber { get; set; }

        /// <summary>Gets or sets <c>Restricted Flag</c> (<c>Y</c>/<c>N</c>). Whether the case is restricted.</summary>
        [JsonPropertyName("Restricted Flag")]
        public string? RestrictedFlag { get; set; }

        /// <summary>Gets or sets <c>MyFS Flag</c>. The document types it text (max 255); <c>N</c> when seen. Read-only.</summary>
        [JsonPropertyName("MyFS Flag")]
        public string? MyFSFlag { get; set; }

        /// <summary>Gets or sets <c>Integration State</c>. E.g. <c>Synced</c>; read-only.</summary>
        [JsonPropertyName("Integration State")]
        public string? IntegrationState { get; set; }

        /// <summary>Gets or sets <c>Assigned To</c>. The login of the assigned worker. Live name; the document has only the id.</summary>
        [JsonPropertyName("Assigned To")]
        public string? AssignedTo { get; set; }

        /// <summary>Gets or sets <c>Assigned To Id</c>. The assigned worker's row id; read-only.</summary>
        [JsonPropertyName("Assigned To Id")]
        public string? AssignedToId { get; set; }

        /// <summary>Gets or sets <c>Created Date</c> (a zone-less date and time). When the case was created. Live name for the document's <c>Created</c>, which ICM rejects in a field list (MEASURED 2026-09-24).</summary>
        [JsonPropertyName("Created Date")]
        public string? CreatedDate { get; set; }

        /// <summary>Gets or sets <c>Created By</c>. The login that created it. Live meaning; the document's <c>Created By</c> is the row id ICM calls <c>Created By Id</c>.</summary>
        [JsonPropertyName("Created By")]
        public string? CreatedBy { get; set; }

        /// <summary>Gets or sets <c>Created By Id</c>. The creating user's row id.</summary>
        [JsonPropertyName("Created By Id")]
        public string? CreatedById { get; set; }

        /// <summary>Gets or sets <c>Updated Date</c> (a zone-less date and time). When the case record was last updated. Live name for the document's <c>Updated</c>.</summary>
        [JsonPropertyName("Updated Date")]
        public string? UpdatedDate { get; set; }

        /// <summary>Gets or sets <c>Updated By</c>. The login that last updated it.</summary>
        [JsonPropertyName("Updated By")]
        public string? UpdatedBy { get; set; }

        /// <summary>Gets or sets <c>Updated By Id</c>. The updating user's row id.</summary>
        [JsonPropertyName("Updated By Id")]
        public string? UpdatedById { get; set; }

        /// <summary>Gets or sets <c>Last Updated Date</c> (a zone-less date and time). <c>DTYPE_DATETIME</c>, zone-less; read-only.</summary>
        [JsonPropertyName("Last Updated Date")]
        public string? LastUpdatedDate { get; set; }

        /// <summary>Gets or sets <c>Key Player Id</c>. The key player's contact row id — the <c>Id</c> of an <c>ICMContact</c> and of a <c>CaseContact</c>; read-only.</summary>
        [JsonPropertyName("Key Player Id")]
        public string? KeyPlayerId { get; set; }

        /// <summary>Gets or sets <c>Key Player Contact Row Num</c>. Despite the name, the key player's <c>Person ID ICM</c> (MEASURED 2026-09-24: <c>1-11069734915</c>, not the row id).</summary>
        [JsonPropertyName("Key Player Contact Row Num")]
        public string? KeyPlayerContactRowNum { get; set; }

        /// <summary>Gets or sets <c>Key Player Integration Id</c>. The key player's <c>Integration Id</c> / MIS person id (max 30).</summary>
        [JsonPropertyName("Key Player Integration Id")]
        public string? KeyPlayerIntegrationId { get; set; }

        /// <summary>Gets or sets <c>Subject Contact First Name</c>. (max 50).</summary>
        [JsonPropertyName("Subject Contact First Name")]
        public string? SubjectContactFirstName { get; set; }

        /// <summary>Gets or sets <c>Subject Contact Last Name</c>. (max 50).</summary>
        [JsonPropertyName("Subject Contact Last Name")]
        public string? SubjectContactLastName { get; set; }

        /// <summary>Gets or sets <c>Middle Name</c>. The subject contact's middle name (max 50).</summary>
        [JsonPropertyName("Middle Name")]
        public string? MiddleName { get; set; }

        /// <summary>Gets or sets <c>Key Player AKA First Name</c>. (max 50).</summary>
        [JsonPropertyName("Key Player AKA First Name")]
        public string? KeyPlayerAKAFirstName { get; set; }

        /// <summary>Gets or sets <c>Key Player AKA Last Name</c>. (max 50).</summary>
        [JsonPropertyName("Key Player AKA Last Name")]
        public string? KeyPlayerAKALastName { get; set; }

        /// <summary>Gets or sets <c>Key Player Birth Date</c> (a date; <c>MM/DD/YYYY</c> when seen). The key player's date of birth.</summary>
        [JsonPropertyName("Key Player Birth Date")]
        public string? KeyPlayerBirthDate { get; set; }

        /// <summary>Gets or sets <c>Key Player M/F</c>. The key player's gender as ICM's own text, e.g. <c>Woman/Girl</c> (max 30).</summary>
        [JsonPropertyName("Key Player M/F")]
        public string? KeyPlayerMF { get; set; }

        /// <summary>Gets or sets <c>Key Player Age</c>. ICM's calculated age, kept as text; read-only.</summary>
        [JsonPropertyName("Key Player Age")]
        public string? KeyPlayerAge { get; set; }

        /// <summary>Gets or sets <c>Key Player Deceased Flag</c> (<c>Y</c>/<c>N</c>). Whether the key player is deceased.</summary>
        [JsonPropertyName("Key Player Deceased Flag")]
        public string? KeyPlayerDeceasedFlag { get; set; }

        /// <summary>Gets or sets <c>Key Player Deceased Date</c> (a date; <c>MM/DD/YYYY</c> when seen). When, if so.</summary>
        [JsonPropertyName("Key Player Deceased Date")]
        public string? KeyPlayerDeceasedDate { get; set; }

        /// <summary>Gets or sets <c>Key Player Created Date</c> (<c>DTYPE_UTCDATETIME</c>). <c>DTYPE_UTCDATETIME</c>; read-only.</summary>
        [JsonPropertyName("Key Player Created Date")]
        public string? KeyPlayerCreatedDate { get; set; }

        /// <summary>Gets or sets <c>Key Player Updated Date</c> (<c>DTYPE_UTCDATETIME</c>). <c>DTYPE_UTCDATETIME</c>; read-only.</summary>
        [JsonPropertyName("Key Player Updated Date")]
        public string? KeyPlayerUpdatedDate { get; set; }

        /// <summary>Gets or sets <c>Key Player Last Updated Date</c> (<c>DTYPE_UTCDATETIME</c>). <c>DTYPE_UTCDATETIME</c>.</summary>
        [JsonPropertyName("Key Player Last Updated Date")]
        public string? KeyPlayerLastUpdatedDate { get; set; }

        /// <summary>
        /// Gets or sets the record's links. Declared so <c>self</c> and <c>canonical</c> do
        /// not land in <see cref="AdditionalFields"/>. Not mapped.
        /// </summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets or sets every field ICM sent that is not declared above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
