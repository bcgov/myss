namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>A contact on a service request, with their relationship to it — a row of the SR's <c>ICMContact</c> child.</summary>
    /// <remarks>
    /// <para>
    /// The contact side of a matched application: the bus pass workflow links the contact
    /// it matched as the primary key player and sets the SR's <c>Primary Contact Id</c>,
    /// which is what fills the SR header's names. <see cref="Id"/> is the contact's own row
    /// id, the same one the contact search returns; the link adds
    /// <see cref="Relationship"/>, <see cref="IsPrimary"/>, <see cref="IsSubject"/> and
    /// <see cref="Start"/>.
    /// </para>
    /// <para>Personal information (names, birth date, DID): never log one whole. Read-only.</para>
    /// </remarks>
    public class ServiceRequestContact
    {
        /// <summary>Gets <c>Id</c>. The contact's row id.</summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>SR Id</c>. The service request's row id.</summary>
        public string? ServiceRequestId { get; init; }

        /// <summary>Gets <c>SR Relationship</c>, e.g. <c>Key player</c>, <c>Spouse</c>.</summary>
        public string? Relationship { get; init; }

        /// <summary>Gets <c>SSA Primary Field</c>. Whether this is the SR's primary contact.</summary>
        public bool? IsPrimary { get; init; }

        /// <summary>Gets <c>SR Subject Flag</c>.</summary>
        public bool? IsSubject { get; init; }

        /// <summary>Gets <c>SR Start</c>, a zone-less date and time as written. Equals the SR's creation time when seen, whenever the link was made.</summary>
        public DateTime? Start { get; init; }

        /// <summary>Gets <c>SR End</c>, a zone-less date and time as written.</summary>
        public DateTime? End { get; init; }

        /// <summary>Gets <c>First Name</c>.</summary>
        public string? FirstName { get; init; }

        /// <summary>Gets <c>Middle Name</c>.</summary>
        public string? MiddleName { get; init; }

        /// <summary>Gets <c>Last Name</c>.</summary>
        public string? LastName { get; init; }

        /// <summary>Gets <c>Birth Date</c>, a date with no time and no zone.</summary>
        public DateOnly? BirthDate { get; init; }

        /// <summary>Gets <c>Age</c>, ICM's calculated age as text.</summary>
        public string? Age { get; init; }

        /// <summary>Gets <c>M/F</c>, the gender as ICM's own text.</summary>
        public string? Gender { get; init; }

        /// <summary>Gets <c>ICM BCSC DID</c>.</summary>
        public string? BcServicesCardDid { get; init; }

        /// <summary>Gets <c>Aboriginal Calc</c>, e.g. <c>TBD</c>.</summary>
        public string? Indigenous { get; init; }

        /// <summary>Gets <c>Created</c>, the contact record's, as a zone-less date and time.</summary>
        public DateTime? ContactCreated { get; init; }

        /// <summary>Gets <c>ICM Created By</c>, the login that created the contact record.</summary>
        public string? ContactCreatedBy { get; init; }

        /// <summary>Gets <c>Updated</c>, the contact record's, as a zone-less date and time.</summary>
        public DateTime? ContactUpdated { get; init; }

        /// <summary>Gets <c>ICM Updated By</c>, the login that last updated the contact record.</summary>
        public string? ContactUpdatedBy { get; init; }

        /// <summary>Gets anything ICM sent that is not modelled above, as raw JSON.</summary>
        public IReadOnlyDictionary<string, JsonElement> AdditionalFields { get; init; } =
            new Dictionary<string, JsonElement>();

        /// <summary>Gets the values that arrived in a shape the client could not read, keyed by ICM field name.</summary>
        public IReadOnlyDictionary<string, string> UnparsedValues { get; init; } =
            new Dictionary<string, string>();
    }
}
