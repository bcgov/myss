namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A row of a service request's <c>ICMContact</c> child collection
    /// (<c>data/ServiceRequest/ServiceRequest/{key}/ICMContact/</c>) as it appears on the
    /// wire: a contact seen through its link to the service request (intersection table
    /// <c>S_SR_CONTACT</c>), described by <c>docs/integration/SRContactLink_OpenApi.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Id"/> is the contact's own row id — the same <c>Id</c> the contact search
    /// returns and a case's <c>Key Player Id</c> names — and the row's audit fields are the
    /// contact's, not the link's (MEASURED SIT2 2026-09-29: <c>Created 09/14/2015</c> on a
    /// link made that day). What the link itself adds is <see cref="SRRelationship"/>,
    /// <see cref="SSAPrimaryField"/>, <see cref="SRSubjectFlag"/> and <see cref="SRStart"/>.
    /// </para>
    /// <para>
    /// The document's 20 names are the live names, plus <c>Id</c>. A different, thinner
    /// component from the case's <c>Contact</c> child and from <c>ICMContact/ICMContact</c>.
    /// </para>
    /// </remarks>
    internal class SiebelServiceRequestContact
    {
        /// <summary>Gets or sets <c>Id</c>. The contact's row id. Not in the document; always sent.</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>SR Id</c>. The service request's row id.</summary>
        [JsonPropertyName("SR Id")]
        public string? SRId { get; set; }

        /// <summary>Gets or sets <c>SR Relationship</c>, e.g. <c>Key player</c>, <c>Spouse</c> (max 30).</summary>
        [JsonPropertyName("SR Relationship")]
        public string? SRRelationship { get; set; }

        /// <summary>Gets or sets <c>SSA Primary Field</c> (<c>Y</c>/<c>N</c>). Whether this is the SR's primary contact.</summary>
        [JsonPropertyName("SSA Primary Field")]
        public string? SSAPrimaryField { get; set; }

        /// <summary>Gets or sets <c>SR Subject Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("SR Subject Flag")]
        public string? SRSubjectFlag { get; set; }

        /// <summary>Gets or sets <c>SR Start</c> (<c>DTYPE_UTCDATETIME</c>). When the contact was linked.</summary>
        [JsonPropertyName("SR Start")]
        public string? SRStart { get; set; }

        /// <summary>Gets or sets <c>SR End</c> (<c>DTYPE_UTCDATETIME</c>).</summary>
        [JsonPropertyName("SR End")]
        public string? SREnd { get; set; }

        /// <summary>Gets or sets <c>First Name</c> (max 50).</summary>
        [JsonPropertyName("First Name")]
        public string? FirstName { get; set; }

        /// <summary>Gets or sets <c>Middle Name</c> (max 50).</summary>
        [JsonPropertyName("Middle Name")]
        public string? MiddleName { get; set; }

        /// <summary>Gets or sets <c>Last Name</c> (max 50).</summary>
        [JsonPropertyName("Last Name")]
        public string? LastName { get; set; }

        /// <summary>Gets or sets <c>Birth Date</c> (<c>DTYPE_DATE</c>).</summary>
        [JsonPropertyName("Birth Date")]
        public string? BirthDate { get; set; }

        /// <summary>Gets or sets <c>Age</c>; read-only, kept as text.</summary>
        [JsonPropertyName("Age")]
        public string? Age { get; set; }

        /// <summary>Gets or sets <c>M/F</c> (max 30).</summary>
        [JsonPropertyName("M/F")]
        public string? MF { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c> (max 255).</summary>
        [JsonPropertyName("ICM BCSC DID")]
        public string? ICMBCSCDID { get; set; }

        /// <summary>Gets or sets <c>Aboriginal Calc</c> (max 255).</summary>
        [JsonPropertyName("Aboriginal Calc")]
        public string? AboriginalCalc { get; set; }

        /// <summary>Gets or sets <c>Created</c>; the contact's, read-only.</summary>
        [JsonPropertyName("Created")]
        public string? Created { get; set; }

        /// <summary>Gets or sets <c>Created By</c>; the contact's, read-only.</summary>
        [JsonPropertyName("Created By")]
        public string? CreatedBy { get; set; }

        /// <summary>Gets or sets <c>ICM Created By</c>, a login; read-only.</summary>
        [JsonPropertyName("ICM Created By")]
        public string? ICMCreatedBy { get; set; }

        /// <summary>Gets or sets <c>Updated</c>; the contact's, read-only.</summary>
        [JsonPropertyName("Updated")]
        public string? Updated { get; set; }

        /// <summary>Gets or sets <c>Updated By</c>; the contact's, read-only.</summary>
        [JsonPropertyName("Updated By")]
        public string? UpdatedBy { get; set; }

        /// <summary>Gets or sets <c>ICM Updated By</c>, a login; read-only.</summary>
        [JsonPropertyName("ICM Updated By")]
        public string? ICMUpdatedBy { get; set; }

        /// <summary>Gets or sets the record's links. Declared so they do not land in <see cref="AdditionalFields"/>. Not mapped.</summary>
        [JsonPropertyName("Link")]
        [JsonConverter(typeof(SiebelLinkListConverter))]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets or sets every field ICM sent that is not declared above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; set; }
    }

    /// <summary>The body of a successful read of a service request's <c>ICMContact</c> child.</summary>
    internal class SiebelServiceRequestContactListResponse
    {
        /// <summary>Gets or sets <c>lastpage</c>, as the string <c>"true"</c>; <see cref="IsLastPage"/> reads it.</summary>
        [JsonPropertyName("lastpage")]
        public string? LastPage { get; set; }

        /// <summary>Gets or sets the rows.</summary>
        [JsonPropertyName("items")]
        public IList<SiebelServiceRequestContact>? Items { get; set; }

        /// <summary>Gets or sets the paging links.</summary>
        [JsonPropertyName("Link")]
        [JsonConverter(typeof(SiebelLinkListConverter))]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets a value indicating whether Siebel said this page is the last one.</summary>
        [JsonIgnore]
        public bool IsLastPage => bool.TryParse(LastPage, out bool last) && last;
    }
}
