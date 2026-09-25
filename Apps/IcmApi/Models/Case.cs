namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;

    /// <summary>An ICM case — a person's file with a ministry program — as MySS needs to know it.</summary>
    /// <remarks>
    /// <para>
    /// The case is the record the legacy INT-331 login call was built around: it names the
    /// program (<see cref="Type"/>), the office, region and organization serving it, its
    /// status, and its key player — the contact whose file it is — by row id, person id and
    /// integration id. The people on the case, with their relationship to it, are a separate
    /// read: <see cref="CaseContact"/>.
    /// </para>
    /// <para>
    /// Field names are what ICM actually sends — MEASURED on SIT2 on 2026-09-24. As with the
    /// service request they differ from <c>docs/integration/Case_OpenApi.json</c> around the
    /// audit fields; the document is still the source of the read-only flags and of the types.
    /// A deliberate subset: the component's child-welfare fields (CSA status, DIN, place of
    /// birth) are not asked for. Read-only: every property is <c>init</c>, and there is no
    /// case write in this library.
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "It is what ICM calls the record, alongside Contact and ServiceRequest; no VB consumer exists.")]
    public class Case
    {
        /// <summary>Gets <c>Id</c>. The row id, the <c>case_key</c> every other call takes. Not in the document; always sent.</summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>Case Num</c>. The case number ICM's screens show (max 100).</summary>
        public string? CaseNumber { get; init; }

        /// <summary>Gets <c>Name</c>. The case name — <c>LAST, FIRST</c> of the key player when seen (max 100).</summary>
        public string? Name { get; init; }

        /// <summary>Gets <c>Type</c>. The program, e.g. <c>Employment and Assistance</c> (max 30).</summary>
        public string? Type { get; init; }

        /// <summary>Gets <c>Status</c>. <c>Open</c> or <c>Closed</c> when seen (max 30).</summary>
        public string? Status { get; init; }

        /// <summary>Gets <c>Close Reason</c>. Why it was closed (max 30).</summary>
        public string? CloseReason { get; init; }

        /// <summary>Gets <c>Closed Date</c>, a date with no time and no zone, read exactly as written. When it was closed.</summary>
        public DateOnly? ClosedDate { get; init; }

        /// <summary>Gets <c>Early Open Reason</c>. (max 30).</summary>
        public string? EarlyOpenReason { get; init; }

        /// <summary>Gets <c>Reopened Date</c>, a date with no time and no zone, read exactly as written. When it was last reopened.</summary>
        public DateOnly? ReopenedDate { get; init; }

        /// <summary>Gets <c>Renew Review Date</c>, a date with no time and no zone, read exactly as written. When the case is next reviewed.</summary>
        public DateOnly? RenewReviewDate { get; init; }

        /// <summary>Gets <c>Caseload</c>. The caseload code (max 3).</summary>
        public string? Caseload { get; init; }

        /// <summary>Gets <c>Work Queue</c>. E.g. <c>SSA-Self Serve Application</c> (max 30).</summary>
        public string? WorkQueue { get; init; }

        /// <summary>Gets <c>Office Name</c>. The ministry office, e.g. <c>106 - Victoria Vefra</c> (max 100).</summary>
        public string? OfficeName { get; init; }

        /// <summary>Gets <c>Region Name</c>. The service region, e.g. <c>MHSD - Vancouver Island</c>; read-only.</summary>
        public string? RegionName { get; init; }

        /// <summary>Gets <c>Organization</c>. The owning organization, e.g. <c>MHSD</c>; read-only.</summary>
        public string? Organization { get; init; }

        /// <summary>Gets <c>Legacy File Number</c>. The pre-ICM file number, e.g. <c>GA04046116</c> (max 15).</summary>
        public string? LegacyFileNumber { get; init; }

        /// <summary>Gets <c>Restricted Flag</c>. Whether the case is restricted.</summary>
        public bool? IsRestricted { get; init; }

        /// <summary>Gets <c>MyFS Flag</c>. The document types it text (max 255); <c>N</c> when seen. Read-only.</summary>
        public string? MyFsFlag { get; init; }

        /// <summary>Gets <c>Integration State</c>. E.g. <c>Synced</c>; read-only.</summary>
        public string? IntegrationState { get; init; }

        /// <summary>Gets <c>Assigned To</c>. The login of the assigned worker. Live name; the document has only the id.</summary>
        public string? AssignedTo { get; init; }

        /// <summary>Gets <c>Assigned To Id</c>. The assigned worker's row id; read-only.</summary>
        public string? AssignedToId { get; init; }

        /// <summary>Gets <c>Created Date</c>, a zone-less date and time exactly as written. When the case was created. Live name for the document's <c>Created</c>, which ICM rejects in a field list (MEASURED 2026-09-24).</summary>
        public DateTime? CreatedDate { get; init; }

        /// <summary>Gets <c>Created By</c>. The login that created it. Live meaning; the document's <c>Created By</c> is the row id ICM calls <c>Created By Id</c>.</summary>
        public string? CreatedBy { get; init; }

        /// <summary>Gets <c>Created By Id</c>. The creating user's row id.</summary>
        public string? CreatedById { get; init; }

        /// <summary>Gets <c>Updated Date</c>, a zone-less date and time exactly as written. When the case record was last updated. Live name for the document's <c>Updated</c>.</summary>
        public DateTime? UpdatedDate { get; init; }

        /// <summary>Gets <c>Updated By</c>. The login that last updated it.</summary>
        public string? UpdatedBy { get; init; }

        /// <summary>Gets <c>Updated By Id</c>. The updating user's row id.</summary>
        public string? UpdatedById { get; init; }

        /// <summary>Gets <c>Last Updated Date</c>, a zone-less date and time exactly as written. <c>DTYPE_DATETIME</c>, zone-less; read-only.</summary>
        public DateTime? LastUpdatedDate { get; init; }

        /// <summary>Gets <c>Key Player Id</c>. The key player's contact row id — the <c>Id</c> of an <c>ICMContact</c> and of a <c>CaseContact</c>; read-only.</summary>
        public string? KeyPlayerId { get; init; }

        /// <summary>Gets <c>Key Player Contact Row Num</c>. Despite the name, the key player's <c>Person ID ICM</c> (MEASURED 2026-09-24: <c>1-11069734915</c>, not the row id).</summary>
        public string? KeyPlayerPersonId { get; init; }

        /// <summary>Gets <c>Key Player Integration Id</c>. The key player's <c>Integration Id</c> / MIS person id (max 30).</summary>
        public string? KeyPlayerIntegrationId { get; init; }

        /// <summary>Gets <c>Subject Contact First Name</c>. (max 50).</summary>
        public string? SubjectFirstName { get; init; }

        /// <summary>Gets <c>Subject Contact Last Name</c>. (max 50).</summary>
        public string? SubjectLastName { get; init; }

        /// <summary>Gets <c>Middle Name</c>. The subject contact's middle name (max 50).</summary>
        public string? SubjectMiddleName { get; init; }

        /// <summary>Gets <c>Key Player AKA First Name</c>. (max 50).</summary>
        public string? KeyPlayerAkaFirstName { get; init; }

        /// <summary>Gets <c>Key Player AKA Last Name</c>. (max 50).</summary>
        public string? KeyPlayerAkaLastName { get; init; }

        /// <summary>Gets <c>Key Player Birth Date</c>, a date with no time and no zone, read exactly as written. The key player's date of birth.</summary>
        public DateOnly? KeyPlayerBirthDate { get; init; }

        /// <summary>Gets <c>Key Player M/F</c>. The key player's gender as ICM's own text, e.g. <c>Woman/Girl</c> (max 30).</summary>
        public string? KeyPlayerGender { get; init; }

        /// <summary>Gets <c>Key Player Age</c>. ICM's calculated age, kept as text; read-only.</summary>
        public string? KeyPlayerAge { get; init; }

        /// <summary>Gets <c>Key Player Deceased Flag</c>. Whether the key player is deceased.</summary>
        public bool? IsKeyPlayerDeceased { get; init; }

        /// <summary>Gets <c>Key Player Deceased Date</c>, a date with no time and no zone, read exactly as written. When, if so.</summary>
        public DateOnly? KeyPlayerDeceasedDate { get; init; }

        /// <summary>Gets <c>Key Player Created Date</c>, read as UTC on the document's word. <c>DTYPE_UTCDATETIME</c>; read-only.</summary>
        public DateTimeOffset? KeyPlayerCreated { get; init; }

        /// <summary>Gets <c>Key Player Updated Date</c>, read as UTC on the document's word. <c>DTYPE_UTCDATETIME</c>; read-only.</summary>
        public DateTimeOffset? KeyPlayerUpdated { get; init; }

        /// <summary>Gets <c>Key Player Last Updated Date</c>, read as UTC on the document's word. <c>DTYPE_UTCDATETIME</c>.</summary>
        public DateTimeOffset? KeyPlayerLastUpdated { get; init; }

        /// <summary>
        /// Gets anything ICM sent that is not modelled above, as raw JSON. Expected to be
        /// empty, since the request names its fields — so anything here means ICM's naming
        /// moved.
        /// </summary>
        public IReadOnlyDictionary<string, JsonElement> AdditionalFields { get; init; } =
            new Dictionary<string, JsonElement>();

        /// <summary>
        /// Gets the values that arrived in a shape the client could not read — a date in a
        /// new format, a flag that is neither <c>Y</c> nor <c>N</c> — keyed by ICM field
        /// name, with the raw text. Never silently dropped.
        /// </summary>
        public IReadOnlyDictionary<string, string> UnparsedValues { get; init; } =
            new Dictionary<string, string>();
    }
}
