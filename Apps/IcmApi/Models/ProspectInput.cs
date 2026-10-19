namespace Icm.Api.Models
{
    using System;

    /// <summary>The fields of a prospect ICM should accept on a create.</summary>
    /// <remarks>
    /// <para>
    /// <b>Untested against a real ICM.</b> Which fields are writable is taken from the
    /// describe document's read-only flags: everything but <c>Created</c>, <c>Created By</c>,
    /// <c>Created By Name</c> and <c>Last Updated By Name</c>, less the other audit fields
    /// (<c>Last Updated</c>, <c>Last Updated By</c>), which Siebel sets itself. The
    /// document also marks <see cref="FirstName"/>, <see cref="LastName"/>,
    /// <see cref="BirthDate"/> and <see cref="Gender"/> required; the library insists on
    /// the first two (and on <see cref="ServiceRequestId"/> for a standalone create, so a
    /// row cannot be filed under no service request) and lets ICM rule on the rest, so a
    /// rejection is ICM's message rather than this library's guess.
    /// </para>
    /// <para>
    /// <b>Only the properties that are set are sent.</b> Null means "leave the field alone".
    /// </para>
    /// <para>
    /// <b>There is no <c>Id</c>.</b> The standalone create is a PUT to the collection, which
    /// Siebel treats as an upsert: a caller-supplied row id would rewrite an existing
    /// applicant row. The placeholder <c>NULL</c> ICM's documentation prescribes is always
    /// sent (and nothing, when nested in a service request write).
    /// </para>
    /// </remarks>
    public class ProspectInput
    {
        /// <summary>
        /// Gets or sets <c>Service Request Id</c>: the row id of the service request the
        /// prospect belongs to. Required for a standalone create; on a create under a service
        /// request it must be null or match the key in the path.
        /// </summary>
        public string? ServiceRequestId { get; set; }

        /// <summary>Gets or sets <c>First Name</c> (max 50). Required.</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets <c>Middle Name</c> (max 50).</summary>
        public string? MiddleName { get; set; }

        /// <summary>Gets or sets <c>Last Name</c> (max 50). Required.</summary>
        public string? LastName { get; set; }

        /// <summary>Gets or sets <c>Alias</c> (max 50).</summary>
        public string? Alias { get; set; }

        /// <summary>Gets or sets <c>Previous First Name</c> (max 50).</summary>
        public string? PreviousFirstName { get; set; }

        /// <summary>Gets or sets <c>Previous Middle Name</c> (max 50).</summary>
        public string? PreviousMiddleName { get; set; }

        /// <summary>Gets or sets <c>Previous Last Name</c> (max 50).</summary>
        public string? PreviousLastName { get; set; }

        /// <summary>Gets or sets <c>Birth Date</c>. Written as <c>MM/DD/YYYY</c>, the format ICM stores it in (MEASURED 2026-09-14).</summary>
        public DateOnly? BirthDate { get; set; }

        /// <summary>Gets or sets <c>M/F</c>, the gender as ICM's own vocabulary (max 30).</summary>
        public string? Gender { get; set; }

        /// <summary>Gets or sets <c>M/M</c>, the salutation id (max 15).</summary>
        public string? Salutation { get; set; }

        /// <summary>Gets or sets <c>Marital Status Code</c> (max 30).</summary>
        public string? MaritalStatusCode { get; set; }

        /// <summary>Gets or sets <c>Social Security Number</c>, the SIN (max 20). Sensitive: never log it.</summary>
        public string? Sin { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c> (max 255).</summary>
        public string? BcServicesCardDid { get; set; }

        /// <summary>Gets or sets <c>Prospective Contact UId</c> (max 100).</summary>
        public string? ProspectiveContactUId { get; set; }

        /// <summary>Gets or sets <c>Applicant Flag</c>.</summary>
        public bool? IsApplicant { get; set; }

        /// <summary>Gets or sets <c>Portal Role</c> (max 30).</summary>
        public string? PortalRole { get; set; }

        /// <summary>Gets or sets <c>Purpose</c> (max 30): <c>Residence/Mailing</c>, <c>Residence</c> or <c>Mailing</c>.</summary>
        public string? Purpose { get; set; }

        /// <summary>Gets or sets <c>Address Care Of</c> (max 20).</summary>
        public string? AddressCareOf { get; set; }

        /// <summary>Gets or sets <c>Unit #</c> (max 5).</summary>
        public string? UnitNumber { get; set; }

        /// <summary>Gets or sets <c>Street Address</c> (max 200).</summary>
        public string? StreetAddress { get; set; }

        /// <summary>Gets or sets <c>Street Address 2</c> (max 100).</summary>
        public string? StreetAddress2 { get; set; }

        /// <summary>Gets or sets <c>Street Address 3</c> (max 100).</summary>
        public string? StreetAddress3 { get; set; }

        /// <summary>Gets or sets <c>City</c> (max 50).</summary>
        public string? City { get; set; }

        /// <summary>Gets or sets <c>State</c>, the province (max 10), e.g. <c>BC</c>.</summary>
        public string? Province { get; set; }

        /// <summary>Gets or sets <c>Postal Code</c> (max 30).</summary>
        public string? PostalCode { get; set; }

        /// <summary>Gets or sets <c>Email Address</c> (max 350).</summary>
        public string? EmailAddress { get; set; }

        /// <summary>Gets or sets <c>Home Phone #</c> (max 40).</summary>
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets <c>Cellular Phone #</c> (max 40).</summary>
        public string? CellularPhone { get; set; }

        /// <summary>Gets or sets <c>Work Phone #</c> (max 40).</summary>
        public string? WorkPhone { get; set; }

        /// <summary>Gets or sets <c>Alternate Phone #</c> (max 40).</summary>
        public string? AlternatePhone { get; set; }

        /// <summary>Gets or sets <c>Preferred Communication Method</c> (max 30).</summary>
        public string? PreferredCommunicationMethod { get; set; }

        /// <summary>Gets or sets <c>Comment</c> (max 2000).</summary>
        public string? Comment { get; set; }

        /// <summary>Gets or sets <c>Source</c> (max 100).</summary>
        public string? Source { get; set; }

        /// <summary>Gets or sets <c>Source Name</c> (max 100).</summary>
        public string? SourceName { get; set; }

        /// <summary>Gets or sets <c>Source Type</c> (max 30).</summary>
        public string? SourceType { get; set; }
    }
}
