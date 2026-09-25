namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>A row of a case's <c>Contact</c> child collection (<c>data/Cases/Case/{case_key}/Contact/</c>) as it appears on the wire — the fields this library asks for.</summary>
    /// <remarks>
    /// <para>
    /// A different business component from <c>ICMContact</c>, with different field names
    /// (<c>Date of Birth</c> here, <c>Birth Date</c> there; <c>Home Phone</c>, not
    /// <c>Home Phone #</c>) and fields the contact search cannot reach: the person's
    /// <c>Relationship</c> to the case, their <c>BCeID User Name</c>, and an address. It shares
    /// the contact's row id, so a row here can be looked up as an <c>ICMContact</c>.
    /// </para>
    /// <para>
    /// There is no describe document for it — <c>Cases/Contact/describe</c> answers
    /// <c>SBL-EAI-50257</c> — so the names are entirely from live responses, MEASURED on SIT1
    /// and SIT2 on 2026-09-24. The component also carries child-welfare fields
    /// (<c>Person Responsible for Alleged Maltreatment</c>, <c>92_1 AGT</c>,
    /// <c>Coordination AGT CA</c>), which are deliberately not asked for.
    /// </para>
    /// </remarks>
    internal class SiebelCaseContact
    {
        /// <summary>Gets or sets <c>Id</c>. The contact's row id — the same <c>Id</c> <c>ICMContact</c> and a case's <c>Key Player Id</c> use (MEASURED 2026-09-24).</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Relationship</c>. The person's relationship to the case, e.g. <c>Key player</c>.</summary>
        [JsonPropertyName("Relationship")]
        public string? Relationship { get; set; }

        /// <summary>Gets or sets <c>Primary</c> (<c>Y</c>/<c>N</c>). Whether this is the case's primary contact.</summary>
        [JsonPropertyName("Primary")]
        public string? Primary { get; set; }

        /// <summary>Gets or sets <c>Start Date</c> (a zone-less date and time). When the person joined the case; zone-less as sent.</summary>
        [JsonPropertyName("Start Date")]
        public string? StartDate { get; set; }

        /// <summary>Gets or sets <c>First Name</c>.</summary>
        [JsonPropertyName("First Name")]
        public string? FirstName { get; set; }

        /// <summary>Gets or sets <c>Last Name</c>.</summary>
        [JsonPropertyName("Last Name")]
        public string? LastName { get; set; }

        /// <summary>Gets or sets <c>Given Names</c>.</summary>
        [JsonPropertyName("Given Names")]
        public string? GivenNames { get; set; }

        /// <summary>Gets or sets <c>AKA First Name</c>.</summary>
        [JsonPropertyName("AKA First Name")]
        public string? AKAFirstName { get; set; }

        /// <summary>Gets or sets <c>AKA Last Name</c>.</summary>
        [JsonPropertyName("AKA Last Name")]
        public string? AKALastName { get; set; }

        /// <summary>Gets or sets <c>Date of Birth</c> (a date; <c>MM/DD/YYYY</c> when seen).</summary>
        [JsonPropertyName("Date of Birth")]
        public string? DateofBirth { get; set; }

        /// <summary>Gets or sets <c>Age</c>. ICM's calculated age, kept as text.</summary>
        [JsonPropertyName("Age")]
        public string? Age { get; set; }

        /// <summary>Gets or sets <c>Gender</c>. As ICM's own text, e.g. <c>Woman/Girl</c>.</summary>
        [JsonPropertyName("Gender")]
        public string? Gender { get; set; }

        /// <summary>Gets or sets <c>SIN</c>. Nine digits, no formatting. Sensitive: never log it.</summary>
        [JsonPropertyName("SIN")]
        public string? SIN { get; set; }

        /// <summary>Gets or sets <c>PHN</c>. Ten digits when seen. Sensitive: never log it.</summary>
        [JsonPropertyName("PHN")]
        public string? PHN { get; set; }

        /// <summary>Gets or sets <c>Person ID ICM</c>. The person number ICM's screens show.</summary>
        [JsonPropertyName("Person ID ICM")]
        public string? PersonIDICM { get; set; }

        /// <summary>Gets or sets <c>Person ID MIS</c>. The legacy MIS person id — the same value as <c>ICMContact</c>'s <c>Integration Id</c>.</summary>
        [JsonPropertyName("Person ID MIS")]
        public string? PersonIDMIS { get; set; }

        /// <summary>Gets or sets <c>BCeID User Name</c>. The BCeID user name on file. The only place a BCeID has been found in ICM's REST surface (MEASURED 2026-09-24); <c>ICMContact</c> has no such field.</summary>
        [JsonPropertyName("BCeID User Name")]
        public string? BCeIDUserName { get; set; }

        /// <summary>Gets or sets <c>Home Phone</c>.</summary>
        [JsonPropertyName("Home Phone")]
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets <c>Street Address</c>. <c>NFA</c> (no fixed address) when seen.</summary>
        [JsonPropertyName("Street Address")]
        public string? StreetAddress { get; set; }

        /// <summary>Gets or sets <c>City</c>.</summary>
        [JsonPropertyName("City")]
        public string? City { get; set; }

        /// <summary>Gets or sets <c>Country</c>.</summary>
        [JsonPropertyName("Country")]
        public string? Country { get; set; }

        /// <summary>Gets or sets <c>Primary Address</c>. The address as one line.</summary>
        [JsonPropertyName("Primary Address")]
        public string? PrimaryAddress { get; set; }

        /// <summary>Gets or sets <c>Citizen</c>. ICM's own text, e.g. <c>Yes</c>.</summary>
        [JsonPropertyName("Citizen")]
        public string? Citizen { get; set; }

        /// <summary>Gets or sets <c>Indigenous</c>. ICM's own text, e.g. <c>TBD</c>.</summary>
        [JsonPropertyName("Indigenous")]
        public string? Indigenous { get; set; }

        /// <summary>Gets or sets <c>Subject</c> (<c>Y</c>/<c>N</c>). Whether the person is the subject of the case.</summary>
        [JsonPropertyName("Subject")]
        public string? Subject { get; set; }

        /// <summary>Gets or sets <c>Subject Child</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Subject Child")]
        public string? SubjectChild { get; set; }

        /// <summary>Gets or sets <c>Parent_Caregiver</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Parent_Caregiver")]
        public string? ParentCaregiver { get; set; }

        /// <summary>Gets or sets <c>Deceased</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Deceased")]
        public string? Deceased { get; set; }

        /// <summary>Gets or sets <c>Potential Duplicate</c> (<c>Y</c>/<c>N</c>). ICM's own note that the person may exist twice.</summary>
        [JsonPropertyName("Potential Duplicate")]
        public string? PotentialDuplicate { get; set; }

        /// <summary>Gets or sets <c>Integration State</c>. E.g. <c>Synced</c>.</summary>
        [JsonPropertyName("Integration State")]
        public string? IntegrationState { get; set; }

        /// <summary>Gets or sets <c>Created Date</c> (a zone-less date and time). When the contact record was created; zone-less as sent.</summary>
        [JsonPropertyName("Created Date")]
        public string? CreatedDate { get; set; }

        /// <summary>Gets or sets <c>Updated Date</c> (a zone-less date and time). When it was last updated; zone-less as sent.</summary>
        [JsonPropertyName("Updated Date")]
        public string? UpdatedDate { get; set; }

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
