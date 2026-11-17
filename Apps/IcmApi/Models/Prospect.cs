namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// A prospect: the applicant row an ICM service request carries before, or instead
    /// of, a matched contact. A bus pass service request has one per address set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The row the bus pass workflow writes from the submitted application (MEASURED SIT2
    /// 2026-09-14): the person's names, birth date, SIN, phones, preferred contact method
    /// and one address, plus <see cref="Purpose"/> saying what the address is for. The
    /// "Validate Address and Promote Prospect" step of the SR's activity plan is where a
    /// worker turns this row into a contact.
    /// </para>
    /// <para>
    /// Field names are the describe document's, which for this component are also the live
    /// ones (see <c>docs/integration/SRProspects_OpenApi.json</c>). All personal
    /// information, SIN included: keep a <see cref="Prospect"/> out of logs. Read-only:
    /// every property is <c>init</c>; a create takes a <see cref="ProspectInput"/>.
    /// </para>
    /// </remarks>
    public class Prospect
    {
        /// <summary>Gets <c>Id</c>. The row id, the <c>srprospects_key</c>. Not in the document; always sent.</summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>Service Request Id</c>. The row id of the service request the prospect belongs to.</summary>
        public string? ServiceRequestId { get; init; }

        /// <summary>Gets <c>First Name</c> (max 50).</summary>
        public string? FirstName { get; init; }

        /// <summary>Gets <c>Middle Name</c> (max 50).</summary>
        public string? MiddleName { get; init; }

        /// <summary>Gets <c>Last Name</c> (max 50).</summary>
        public string? LastName { get; init; }

        /// <summary>Gets <c>Alias</c> (max 50). On the standalone path only.</summary>
        public string? Alias { get; init; }

        /// <summary>Gets <c>Previous First Name</c> (max 50).</summary>
        public string? PreviousFirstName { get; init; }

        /// <summary>Gets <c>Previous Middle Name</c> (max 50).</summary>
        public string? PreviousMiddleName { get; init; }

        /// <summary>Gets <c>Previous Last Name</c> (max 50).</summary>
        public string? PreviousLastName { get; init; }

        /// <summary>Gets <c>Birth Date</c>, a date with no time and no zone, read exactly as written.</summary>
        public DateOnly? BirthDate { get; init; }

        /// <summary>Gets <c>M/F</c>. The gender as ICM's own text (max 30); empty on every workflow-created row seen.</summary>
        public string? Gender { get; init; }

        /// <summary>Gets <c>M/M</c>. The salutation, an id (max 15); never seen with a value.</summary>
        public string? Salutation { get; init; }

        /// <summary>Gets <c>Marital Status Code</c> (max 30).</summary>
        public string? MaritalStatusCode { get; init; }

        /// <summary>Gets <c>Social Security Number</c>. The SIN, nine digits when seen. Sensitive: never log it.</summary>
        public string? Sin { get; init; }

        /// <summary>Gets <c>ICM BCSC DID</c>. The BC Services Card DID (max 255).</summary>
        public string? BcServicesCardDid { get; init; }

        /// <summary>Gets <c>Prospective Contact UId</c> (max 100). On the standalone path only.</summary>
        public string? ProspectiveContactUId { get; init; }

        /// <summary>Gets <c>Applicant Flag</c>. <c>N</c> on every workflow-created row seen.</summary>
        public bool? IsApplicant { get; init; }

        /// <summary>Gets <c>Portal Role</c> (max 30), e.g. <c>Key player</c>.</summary>
        public string? PortalRole { get; init; }

        /// <summary>Gets <c>Purpose</c> (max 30). What the address is for: <c>Residence/Mailing</c> or <c>Residence</c> when seen.</summary>
        public string? Purpose { get; init; }

        /// <summary>Gets <c>Address Care Of</c> (max 20).</summary>
        public string? AddressCareOf { get; init; }

        /// <summary>Gets <c>Unit #</c> (max 5).</summary>
        public string? UnitNumber { get; init; }

        /// <summary>Gets <c>Street Address</c> (max 200).</summary>
        public string? StreetAddress { get; init; }

        /// <summary>Gets <c>Street Address 2</c> (max 100).</summary>
        public string? StreetAddress2 { get; init; }

        /// <summary>Gets <c>Street Address 3</c> (max 100).</summary>
        public string? StreetAddress3 { get; init; }

        /// <summary>Gets <c>City</c> (max 50).</summary>
        public string? City { get; init; }

        /// <summary>Gets <c>State</c>. The province, <c>BC</c> when seen (max 10).</summary>
        public string? Province { get; init; }

        /// <summary>Gets <c>Postal Code</c> (max 30).</summary>
        public string? PostalCode { get; init; }

        /// <summary>Gets <c>Email Address</c> (max 350).</summary>
        public string? EmailAddress { get; init; }

        /// <summary>Gets <c>Home Phone #</c> (max 40).</summary>
        public string? HomePhone { get; init; }

        /// <summary>Gets <c>Cellular Phone #</c> (max 40).</summary>
        public string? CellularPhone { get; init; }

        /// <summary>Gets <c>Work Phone #</c> (max 40).</summary>
        public string? WorkPhone { get; init; }

        /// <summary>Gets <c>Alternate Phone #</c> (max 40). Where the old form's leave-message consent lands (MEASURED 2026-09-14).</summary>
        public string? AlternatePhone { get; init; }

        /// <summary>Gets <c>Preferred Communication Method</c> (max 30), e.g. <c>Home Phone</c>, <c>Cell Phone</c>, <c>Email</c>.</summary>
        public string? PreferredCommunicationMethod { get; init; }

        /// <summary>Gets <c>Comment</c> (max 2000). On the standalone path only.</summary>
        public string? Comment { get; init; }

        /// <summary>Gets <c>Source</c> (max 100). On the standalone path only.</summary>
        public string? Source { get; init; }

        /// <summary>Gets <c>Source Name</c> (max 100). On the standalone path only.</summary>
        public string? SourceName { get; init; }

        /// <summary>Gets <c>Source Type</c> (max 30). On the standalone path only.</summary>
        public string? SourceType { get; init; }

        /// <summary>
        /// Gets <c>Created</c>, a zone-less date and time exactly as written. The document
        /// types it UTC, but the value is the same wall clock as the parent SR's zone-less
        /// <c>Created Date</c> (MEASURED 2026-09-29: prospect <c>12:07:36</c> on an SR created
        /// <c>12:07:35</c>, local time), so it is kept as written rather than shifted.
        /// </summary>
        public DateTime? Created { get; init; }

        /// <summary>Gets <c>Created By</c>. The creating user's row id, e.g. <c>1-A6IS</c>.</summary>
        public string? CreatedById { get; init; }

        /// <summary>Gets <c>Created By Name</c>. The creating user's login, e.g. <c>SIEBEL_EAI</c>.</summary>
        public string? CreatedBy { get; init; }

        /// <summary>Gets <c>Last Updated</c>, a zone-less date and time exactly as written, for the reason <see cref="Created"/> gives.</summary>
        public DateTime? LastUpdated { get; init; }

        /// <summary>Gets <c>Last Updated By</c>. The updating user's row id.</summary>
        public string? LastUpdatedById { get; init; }

        /// <summary>Gets <c>Last Updated By Name</c>. The updating user's login.</summary>
        public string? LastUpdatedBy { get; init; }

        /// <summary>
        /// Gets anything ICM sent that is not modelled above, as raw JSON. Expected to be
        /// empty; anything here means ICM's naming moved or the component grew a field.
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
