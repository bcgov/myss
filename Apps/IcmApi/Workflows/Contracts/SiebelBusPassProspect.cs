namespace Icm.Api.Workflows.Contracts
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One <c>SRProspects</c> row — the applicant, in Siebel's abbreviated field names
    /// (<c>FstNme</c>, <c>StAdd</c>, phone fields ending in <c>#</c>).
    /// </summary>
    /// <remarks>
    /// "Prospect" is Siebel's term for a person who may not yet be a contact — which fits
    /// this workflow, where a new applicant has no ICM record for the workflow to match.
    /// The row carries exactly one address set; how a differing mailing address is meant to
    /// be represented (a second row? not at all?) is an open question recorded in the
    /// README, and until it is answered the mapper sends one row with the residential
    /// address.
    /// </remarks>
    internal class SiebelBusPassProspect
    {
        /// <summary>Gets or sets <c>AlternatePhone#</c>.</summary>
        [JsonPropertyName("AlternatePhone#")]
        public string? AlternatePhone { get; set; }

        /// <summary>Gets or sets <c>CellularPhone#</c>.</summary>
        [JsonPropertyName("CellularPhone#")]
        public string? CellularPhone { get; set; }

        /// <summary>Gets or sets <c>HomePhone#</c>.</summary>
        [JsonPropertyName("HomePhone#")]
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets <c>Unit#</c>.</summary>
        [JsonPropertyName("Unit#")]
        public string? Unit { get; set; }

        /// <summary>Gets or sets <c>WorkPhone#</c>.</summary>
        [JsonPropertyName("WorkPhone#")]
        public string? WorkPhone { get; set; }

        /// <summary>Gets or sets <c>ProspectKey</c>.</summary>
        [JsonPropertyName("ProspectKey")]
        public string? ProspectKey { get; set; }

        /// <summary>Gets or sets <c>FstNme</c> (first name).</summary>
        [JsonPropertyName("FstNme")]
        public string? FstNme { get; set; }

        /// <summary>Gets or sets <c>LstNme</c> (last name).</summary>
        [JsonPropertyName("LstNme")]
        public string? LstNme { get; set; }

        /// <summary>Gets or sets <c>DOB</c> (text on the wire; format unconfirmed).</summary>
        [JsonPropertyName("DOB")]
        public string? DOB { get; set; }

        /// <summary>Gets or sets <c>Phone</c>, the untyped phone field.</summary>
        [JsonPropertyName("Phone")]
        public string? Phone { get; set; }

        /// <summary>Gets or sets <c>StAdd</c> (street address line 1).</summary>
        [JsonPropertyName("StAdd")]
        public string? StAdd { get; set; }

        /// <summary>Gets or sets <c>FreeText</c>.</summary>
        [JsonPropertyName("FreeText")]
        public string? FreeText { get; set; }

        /// <summary>Gets or sets <c>MethodOfCommunication</c>.</summary>
        [JsonPropertyName("MethodOfCommunication")]
        public string? MethodOfCommunication { get; set; }

        /// <summary>Gets or sets <c>StAdd2</c> (street address line 2).</summary>
        [JsonPropertyName("StAdd2")]
        public string? StAdd2 { get; set; }

        /// <summary>Gets or sets <c>BusPassRequestType</c>.</summary>
        [JsonPropertyName("BusPassRequestType")]
        public string? BusPassRequestType { get; set; }

        /// <summary>Gets or sets <c>City</c>.</summary>
        [JsonPropertyName("City")]
        public string? City { get; set; }

        /// <summary>Gets or sets <c>Prov</c> (province).</summary>
        [JsonPropertyName("Prov")]
        public string? Prov { get; set; }

        /// <summary>Gets or sets <c>Postal</c> (postal code).</summary>
        [JsonPropertyName("Postal")]
        public string? Postal { get; set; }

        /// <summary>Gets or sets <c>SIN</c>.</summary>
        [JsonPropertyName("SIN")]
        public string? SIN { get; set; }

        /// <summary>Gets or sets <c>Role</c>.</summary>
        [JsonPropertyName("Role")]
        public string? Role { get; set; }

        /// <summary>Gets or sets <c>ClientId</c>.</summary>
        [JsonPropertyName("ClientId")]
        public string? ClientId { get; set; }

        /// <summary>Gets or sets <c>EmailAddress</c>.</summary>
        [JsonPropertyName("EmailAddress")]
        public string? EmailAddress { get; set; }
    }
}
