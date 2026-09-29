namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>An ICM contact — a person — as MySS needs to know them.</summary>
    /// <remarks>
    /// <para>
    /// <b>A deliberate subset, and the subset is what is asked for.</b> ICM's contact
    /// business component has 80 fields and serves every ministry program that uses ICM, so
    /// it carries ethnicity, religious affiliation, medications, a child's doctor — nothing
    /// an income-assistance portal has any reason to hold. A search names exactly the
    /// fields below in its <c>fields</c> parameter, so the rest never leave ICM; they are
    /// not fetched and then ignored. SIN and PHN <i>are</i> among them — the legacy
    /// INT-331 tombstone returned the SIN, and identity confirmation needs both — which
    /// makes every <see cref="Contact"/> something to keep out of logs and caches that
    /// were not designed to hold them.
    /// </para>
    /// <para>
    /// Field names are what ICM actually sends — MEASURED against SIT1 on 2026-09-17. For
    /// this component they match <c>docs/integration/Contact_OpenApi.json</c> almost
    /// everywhere (the service request's do not), with two exceptions: ICM sends an
    /// <c>Id</c> the document does not declare, and sends <c>Primary Email</c> where the
    /// document says <c>Email Address</c>.
    /// </para>
    /// <para>
    /// Read-only: every property is <c>init</c>. There is no contact write in this
    /// library.
    /// </para>
    /// </remarks>
    public class Contact
    {
        /// <summary>
        /// Gets <c>Id</c>, the ICM row id — the handle other ICM records refer to a contact
        /// by (a service request's <c>Primary Contact Id</c>, for one).
        /// </summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>Person ID ICM</c> (max 100), the person number ICM's screens show.</summary>
        public string? PersonId { get; init; }

        /// <summary>Gets <c>Integration Id</c> (max 30).</summary>
        public string? IntegrationId { get; init; }

        /// <summary>Gets <c>ICM BCSC DID</c> (max 255), the BC Services Card DID on file.</summary>
        public string? BcServicesCardDid { get; init; }

        /// <summary>
        /// Gets <c>SIN</c> (max 20). Nine digits with no spaces or dashes, as ICM stores it
        /// (MEASURED). Sensitive: never log it.
        /// </summary>
        public string? Sin { get; init; }

        /// <summary>
        /// Gets <c>PHN</c> (max 20). No record seen so far carries one, so its stored
        /// format is unknown. Sensitive: never log it.
        /// </summary>
        public string? Phn { get; init; }

        /// <summary>Gets <c>First Name</c> (max 50).</summary>
        public string? FirstName { get; init; }

        /// <summary>Gets <c>Middle Name</c> (max 50).</summary>
        public string? MiddleName { get; init; }

        /// <summary>Gets <c>Last Name</c> (max 50).</summary>
        public string? LastName { get; init; }

        /// <summary>
        /// Gets <c>Birth Date</c>. A date with no time and no zone, read exactly as written
        /// — see <see cref="ServiceRequest"/> for why that matters west of Greenwich.
        /// </summary>
        public DateOnly? BirthDate { get; init; }

        /// <summary>Gets <c>M/F</c> (max 30), the gender on file, as ICM's own text.</summary>
        public string? Gender { get; init; }

        /// <summary>
        /// Gets <c>Primary Email</c>. The document calls this <c>Email Address</c> (max
        /// 350); ICM rejects that name in a field list.
        /// </summary>
        public string? Email { get; init; }

        /// <summary>Gets <c>Cellular Phone #</c> (max 40).</summary>
        public string? CellPhone { get; init; }

        /// <summary>Gets <c>Home Phone #</c> (max 40).</summary>
        public string? HomePhone { get; init; }

        /// <summary>Gets <c>Work Phone #</c> (max 40).</summary>
        public string? WorkPhone { get; init; }

        /// <summary>Gets <c>Message Phone</c> (max 40).</summary>
        public string? MessagePhone { get; init; }

        /// <summary>Gets <c>Deceased Flag</c>. Read-only in ICM.</summary>
        public bool? IsDeceased { get; init; }

        /// <summary>
        /// Gets <c>Potential Duplicate Flag</c> — ICM's own note that this person may exist
        /// twice. Worth a look before treating a search's single result as the only record.
        /// </summary>
        public bool? IsPotentialDuplicate { get; init; }

        /// <summary>
        /// Gets <c>Created</c>. Read-only in ICM. The document types it
        /// <c>DTYPE_UTCDATETIME</c> and the wire carries no offset, so it is read as UTC on
        /// the document's word — not yet checked against the Siebel UI the way the service
        /// request's dates were.
        /// </summary>
        public DateTimeOffset? Created { get; init; }

        /// <summary>Gets <c>Updated</c>. Read-only in ICM. UTC on the same basis as <see cref="Created"/>.</summary>
        public DateTimeOffset? Updated { get; init; }

        /// <summary>
        /// Gets anything ICM sent that is not modelled above, as raw JSON. Expected to be
        /// empty, since the request names its fields — so anything here means ICM's naming
        /// moved, which is how the service request's mismatch was found.
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
