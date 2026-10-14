namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A Siebel <c>Prospects/SRProspects</c> record as it appears on the wire — the applicant
    /// row a bus pass service request carries — as described by
    /// <c>docs/integration/SRProspects_OpenApi.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The document's names are the live names</b>, which is not true of the service
    /// request or the case: MEASURED on SIT2 on 2026-09-14 and 2026-09-29, a prospect row
    /// read under its service request (<c>…/ServiceRequest/{key}/SRProspects/</c>) carries
    /// 36 of the document's 42 fields under exactly the document's spelling, plus <c>Id</c>,
    /// which no document declares. The six the child view does not send — <c>Alias</c>,
    /// <c>Comment</c>, <c>Prospective Contact UId</c>, <c>Source</c>, <c>Source Name</c>,
    /// <c>Source Type</c> — are declared here from the document, for the standalone path.
    /// </para>
    /// <para>
    /// The same type is the body of a create: every property is nullable, unset ones are
    /// omitted (<see cref="IcmRefitSettings.JsonOptions"/>), and <see cref="ProspectMapper"/>
    /// decides which fields a caller may set. Everything is a string because that is what
    /// Siebel sends and takes; typing it is the mapper's job. <see cref="AdditionalFields"/>
    /// catches anything not listed, so a renamed field surfaces as data rather than as a
    /// property that is null for ever.
    /// </para>
    /// </remarks>
    internal class SiebelProspect
    {
        /// <summary>Gets or sets <c>Id</c>. The row id, the <c>srprospects_key</c>. Not in the document; always sent.</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Service Request Id</c>. The row id of the service request the prospect belongs to (<c>DTYPE_ID</c>, max 15).</summary>
        [JsonPropertyName("Service Request Id")]
        public string? ServiceRequestId { get; set; }

        /// <summary>Gets or sets <c>First Name</c> (max 50). Required by the document.</summary>
        [JsonPropertyName("First Name")]
        public string? FirstName { get; set; }

        /// <summary>Gets or sets <c>Middle Name</c> (max 50).</summary>
        [JsonPropertyName("Middle Name")]
        public string? MiddleName { get; set; }

        /// <summary>Gets or sets <c>Last Name</c> (max 50). Required by the document.</summary>
        [JsonPropertyName("Last Name")]
        public string? LastName { get; set; }

        /// <summary>Gets or sets <c>Alias</c> (max 50). Document only; not sent by the child view.</summary>
        [JsonPropertyName("Alias")]
        public string? Alias { get; set; }

        /// <summary>Gets or sets <c>Previous First Name</c> (max 50).</summary>
        [JsonPropertyName("Previous First Name")]
        public string? PreviousFirstName { get; set; }

        /// <summary>Gets or sets <c>Previous Middle Name</c> (max 50).</summary>
        [JsonPropertyName("Previous Middle Name")]
        public string? PreviousMiddleName { get; set; }

        /// <summary>Gets or sets <c>Previous Last Name</c> (max 50).</summary>
        [JsonPropertyName("Previous Last Name")]
        public string? PreviousLastName { get; set; }

        /// <summary>Gets or sets <c>Birth Date</c> (<c>DTYPE_DATE</c>; <c>MM/DD/YYYY</c> when seen). Required by the document.</summary>
        [JsonPropertyName("Birth Date")]
        public string? BirthDate { get; set; }

        /// <summary>Gets or sets <c>M/F</c>. The gender, as ICM's own text (max 30). Required by the document, yet empty on every workflow-created row seen.</summary>
        [JsonPropertyName("M/F")]
        public string? MF { get; set; }

        /// <summary>Gets or sets <c>M/M</c>. The salutation, typed <c>DTYPE_ID</c> (max 15); never seen with a value.</summary>
        [JsonPropertyName("M/M")]
        public string? MM { get; set; }

        /// <summary>Gets or sets <c>Marital Status Code</c> (max 30).</summary>
        [JsonPropertyName("Marital Status Code")]
        public string? MaritalStatusCode { get; set; }

        /// <summary>Gets or sets <c>Social Security Number</c>. The SIN, nine digits when seen (max 20). Sensitive: never log it.</summary>
        [JsonPropertyName("Social Security Number")]
        public string? SocialSecurityNumber { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c>. The BC Services Card DID (max 255).</summary>
        [JsonPropertyName("ICM BCSC DID")]
        public string? ICMBCSCDID { get; set; }

        /// <summary>Gets or sets <c>Prospective Contact UId</c> (max 100). Document only; not sent by the child view.</summary>
        [JsonPropertyName("Prospective Contact UId")]
        public string? ProspectiveContactUId { get; set; }

        /// <summary>Gets or sets <c>Applicant Flag</c> (<c>Y</c>/<c>N</c>). <c>N</c> on every workflow-created row seen.</summary>
        [JsonPropertyName("Applicant Flag")]
        public string? ApplicantFlag { get; set; }

        /// <summary>Gets or sets <c>Portal Role</c> (max 30). <c>Key player</c> when seen on an income assistance SR.</summary>
        [JsonPropertyName("Portal Role")]
        public string? PortalRole { get; set; }

        /// <summary>Gets or sets <c>Purpose</c> (max 30). What the address is for: <c>Residence/Mailing</c> or <c>Residence</c> when seen.</summary>
        [JsonPropertyName("Purpose")]
        public string? Purpose { get; set; }

        /// <summary>Gets or sets <c>Address Care Of</c> (max 20).</summary>
        [JsonPropertyName("Address Care Of")]
        public string? AddressCareOf { get; set; }

        /// <summary>Gets or sets <c>Unit #</c> (max 5).</summary>
        [JsonPropertyName("Unit #")]
        public string? UnitNumber { get; set; }

        /// <summary>Gets or sets <c>Street Address</c> (max 200).</summary>
        [JsonPropertyName("Street Address")]
        public string? StreetAddress { get; set; }

        /// <summary>Gets or sets <c>Street Address 2</c> (max 100).</summary>
        [JsonPropertyName("Street Address 2")]
        public string? StreetAddress2 { get; set; }

        /// <summary>Gets or sets <c>Street Address 3</c> (max 100).</summary>
        [JsonPropertyName("Street Address 3")]
        public string? StreetAddress3 { get; set; }

        /// <summary>Gets or sets <c>City</c> (max 50).</summary>
        [JsonPropertyName("City")]
        public string? City { get; set; }

        /// <summary>Gets or sets <c>State</c>. The province, <c>BC</c> when seen (max 10).</summary>
        [JsonPropertyName("State")]
        public string? State { get; set; }

        /// <summary>Gets or sets <c>Postal Code</c> (max 30).</summary>
        [JsonPropertyName("Postal Code")]
        public string? PostalCode { get; set; }

        /// <summary>Gets or sets <c>Email Address</c> (max 350; the document validates it as <c>*?@*?.?*</c>).</summary>
        [JsonPropertyName("Email Address")]
        public string? EmailAddress { get; set; }

        /// <summary>Gets or sets <c>Home Phone #</c> (<c>DTYPE_PHONE</c>, max 40).</summary>
        [JsonPropertyName("Home Phone #")]
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets <c>Cellular Phone #</c> (<c>DTYPE_PHONE</c>, max 40).</summary>
        [JsonPropertyName("Cellular Phone #")]
        public string? CellularPhone { get; set; }

        /// <summary>Gets or sets <c>Work Phone #</c> (<c>DTYPE_PHONE</c>, max 40).</summary>
        [JsonPropertyName("Work Phone #")]
        public string? WorkPhone { get; set; }

        /// <summary>Gets or sets <c>Alternate Phone #</c> (<c>DTYPE_PHONE</c>, max 40). The old form's leave-message consent lands here (MEASURED 2026-09-14).</summary>
        [JsonPropertyName("Alternate Phone #")]
        public string? AlternatePhone { get; set; }

        /// <summary>Gets or sets <c>Preferred Communication Method</c> (max 30), e.g. <c>Home Phone</c>, <c>Cell Phone</c>, <c>Email</c>.</summary>
        [JsonPropertyName("Preferred Communication Method")]
        public string? PreferredCommunicationMethod { get; set; }

        /// <summary>Gets or sets <c>Comment</c> (max 2000). Document only; not sent by the child view.</summary>
        [JsonPropertyName("Comment")]
        public string? Comment { get; set; }

        /// <summary>Gets or sets <c>Source</c> (max 100). Document only; not sent by the child view.</summary>
        [JsonPropertyName("Source")]
        public string? Source { get; set; }

        /// <summary>Gets or sets <c>Source Name</c> (max 100). Document only; not sent by the child view.</summary>
        [JsonPropertyName("Source Name")]
        public string? SourceName { get; set; }

        /// <summary>Gets or sets <c>Source Type</c> (max 30). Document only; not sent by the child view.</summary>
        [JsonPropertyName("Source Type")]
        public string? SourceType { get; set; }

        /// <summary>Gets or sets <c>Created</c>. Typed <c>DTYPE_UTCDATETIME</c> by the document; read-only.</summary>
        [JsonPropertyName("Created")]
        public string? Created { get; set; }

        /// <summary>Gets or sets <c>Created By</c>. The creating user's row id (<c>DTYPE_ID</c>); read-only.</summary>
        [JsonPropertyName("Created By")]
        public string? CreatedBy { get; set; }

        /// <summary>Gets or sets <c>Created By Name</c>. The creating user's login, e.g. <c>SIEBEL_EAI</c>; read-only.</summary>
        [JsonPropertyName("Created By Name")]
        public string? CreatedByName { get; set; }

        /// <summary>Gets or sets <c>Last Updated</c>. Typed <c>DTYPE_UTCDATETIME</c> by the document.</summary>
        [JsonPropertyName("Last Updated")]
        public string? LastUpdated { get; set; }

        /// <summary>Gets or sets <c>Last Updated By</c>. The updating user's row id (<c>DTYPE_ID</c>).</summary>
        [JsonPropertyName("Last Updated By")]
        public string? LastUpdatedBy { get; set; }

        /// <summary>Gets or sets <c>Last Updated By Name</c>. The updating user's login; read-only.</summary>
        [JsonPropertyName("Last Updated By Name")]
        public string? LastUpdatedByName { get; set; }

        /// <summary>
        /// Gets or sets the record's links. Declared so <c>self</c>, <c>canonical</c> and
        /// <c>parent</c> do not land in <see cref="AdditionalFields"/>. Not mapped.
        /// </summary>
        [JsonPropertyName("Link")]
        [JsonConverter(typeof(SiebelLinkListConverter))]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets or sets every field ICM sent that is not declared above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
