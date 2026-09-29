namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A Siebel ICMContact record as it appears on the wire — the fields this library asks
    /// for, not the 80 the business component has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Names are from real responses.</b> MEASURED against SIT1 on 2026-09-17 over
    /// records carrying a BCSC DID: unlike the service request, the live names match
    /// <c>docs/integration/Contact_OpenApi.json</c> nearly throughout. The two exceptions
    /// are <c>Id</c>, which ICM sends and the document does not declare, and
    /// <c>Primary Email</c>, which the document calls <c>Email Address</c>.
    /// </para>
    /// <para>
    /// <b>Two vocabularies, as on the service request.</b> The <c>fields</c> parameter
    /// takes the live names — <c>fields=Email Address</c> is rejected with
    /// <c>SBL-EAI-50258</c> — while <c>searchspec</c> takes the business component's, so
    /// <c>[Primary Email]</c> is rejected there with <c>SBL-DAT-00416</c>.
    /// <see cref="ContactMapper"/> holds both lists.
    /// </para>
    /// <para>
    /// Everything is a nullable string because that is what Siebel sends; typing it is the
    /// mapper's job. <see cref="AdditionalFields"/> catches anything not listed, so a
    /// renamed field surfaces as data rather than as a property that is null for ever.
    /// </para>
    /// </remarks>
    internal class SiebelContact
    {
        /// <summary>Gets or sets <c>Id</c>, the row id. Not in the document; always sent.</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Person ID ICM</c> (max 100).</summary>
        [JsonPropertyName("Person ID ICM")]
        public string? PersonIdIcm { get; set; }

        /// <summary>Gets or sets <c>Integration Id</c> (max 30).</summary>
        [JsonPropertyName("Integration Id")]
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c> (max 255; 44 characters of base64 when seen).</summary>
        [JsonPropertyName("ICM BCSC DID")]
        public string? IcmBcscDid { get; set; }

        /// <summary>Gets or sets <c>SIN</c> (max 20; nine unformatted digits when seen).</summary>
        [JsonPropertyName("SIN")]
        public string? Sin { get; set; }

        /// <summary>Gets or sets <c>PHN</c> (max 20; never yet seen with a value).</summary>
        [JsonPropertyName("PHN")]
        public string? Phn { get; set; }

        /// <summary>Gets or sets <c>First Name</c> (max 50).</summary>
        [JsonPropertyName("First Name")]
        public string? FirstName { get; set; }

        /// <summary>Gets or sets <c>Middle Name</c> (max 50).</summary>
        [JsonPropertyName("Middle Name")]
        public string? MiddleName { get; set; }

        /// <summary>Gets or sets <c>Last Name</c> (max 50).</summary>
        [JsonPropertyName("Last Name")]
        public string? LastName { get; set; }

        /// <summary>Gets or sets <c>Birth Date</c> (a date; seen as <c>MM/DD/YYYY</c>).</summary>
        [JsonPropertyName("Birth Date")]
        public string? BirthDate { get; set; }

        /// <summary>Gets or sets <c>M/F</c> (max 30).</summary>
        [JsonPropertyName("M/F")]
        public string? MF { get; set; }

        /// <summary>Gets or sets <c>Primary Email</c> — the document's <c>Email Address</c> (max 350).</summary>
        [JsonPropertyName("Primary Email")]
        public string? PrimaryEmail { get; set; }

        /// <summary>Gets or sets <c>Cellular Phone #</c> (max 40).</summary>
        [JsonPropertyName("Cellular Phone #")]
        public string? CellularPhone { get; set; }

        /// <summary>Gets or sets <c>Home Phone #</c> (max 40).</summary>
        [JsonPropertyName("Home Phone #")]
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets <c>Work Phone #</c> (max 40).</summary>
        [JsonPropertyName("Work Phone #")]
        public string? WorkPhone { get; set; }

        /// <summary>Gets or sets <c>Message Phone</c> (max 40).</summary>
        [JsonPropertyName("Message Phone")]
        public string? MessagePhone { get; set; }

        /// <summary>Gets or sets <c>Deceased Flag</c> (<c>Y</c>/<c>N</c>; read-only).</summary>
        [JsonPropertyName("Deceased Flag")]
        public string? DeceasedFlag { get; set; }

        /// <summary>Gets or sets <c>Potential Duplicate Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Potential Duplicate Flag")]
        public string? PotentialDuplicateFlag { get; set; }

        /// <summary>
        /// Gets or sets <c>Created</c> (<c>DTYPE_UTCDATETIME</c>; read-only; seen as
        /// <c>MM/DD/YYYY HH:MM:SS</c> with no offset).
        /// </summary>
        [JsonPropertyName("Created")]
        public string? Created { get; set; }

        /// <summary>Gets or sets <c>Updated</c> (<c>DTYPE_UTCDATETIME</c>; read-only).</summary>
        [JsonPropertyName("Updated")]
        public string? Updated { get; set; }

        /// <summary>
        /// Gets or sets the record's links. ICM sends <c>self</c> and <c>canonical</c> even
        /// with <c>childlinks=None</c>; declared so they do not land in
        /// <see cref="AdditionalFields"/> and drown out a real surprise. Not mapped.
        /// </summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets or sets every field ICM sent that is not declared above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
