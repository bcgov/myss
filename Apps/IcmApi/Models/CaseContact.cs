namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;

    /// <summary>A person on an ICM case, with their relationship to it.</summary>
    /// <remarks>
    /// <para>
    /// Read from the case's <c>Contact</c> child collection, which is the only REST surface
    /// found so far that says who is on a case and how — and the only one carrying a
    /// <see cref="BceidUserName"/> (MEASURED on SIT2 on 2026-09-24). Its <see cref="Id"/>
    /// is the same row id the contact search returns, so the two can be joined.
    /// </para>
    /// <para>
    /// All personal information, SIN and PHN included: keep a <see cref="CaseContact"/> out
    /// of logs and of any cache not designed to hold it. Read-only: every property is
    /// <c>init</c>.
    /// </para>
    /// </remarks>
    public class CaseContact
    {
        /// <summary>Gets <c>Id</c>. The contact's row id — the same <c>Id</c> <c>ICMContact</c> and a case's <c>Key Player Id</c> use (MEASURED 2026-09-24).</summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>Relationship</c>. The person's relationship to the case, e.g. <c>Key player</c>.</summary>
        public string? Relationship { get; init; }

        /// <summary>Gets <c>Primary</c>. Whether this is the case's primary contact.</summary>
        public bool? IsPrimary { get; init; }

        /// <summary>Gets <c>Start Date</c>, a zone-less date and time exactly as written. When the person joined the case; zone-less as sent.</summary>
        public DateTime? StartDate { get; init; }

        /// <summary>Gets <c>First Name</c>.</summary>
        public string? FirstName { get; init; }

        /// <summary>Gets <c>Last Name</c>.</summary>
        public string? LastName { get; init; }

        /// <summary>Gets <c>Given Names</c>.</summary>
        public string? GivenNames { get; init; }

        /// <summary>Gets <c>AKA First Name</c>.</summary>
        public string? AkaFirstName { get; init; }

        /// <summary>Gets <c>AKA Last Name</c>.</summary>
        public string? AkaLastName { get; init; }

        /// <summary>Gets <c>Date of Birth</c>, a date with no time and no zone, read exactly as written.</summary>
        public DateOnly? BirthDate { get; init; }

        /// <summary>Gets <c>Age</c>. ICM's calculated age, kept as text.</summary>
        public string? Age { get; init; }

        /// <summary>Gets <c>Gender</c>. As ICM's own text, e.g. <c>Woman/Girl</c>.</summary>
        public string? Gender { get; init; }

        /// <summary>Gets <c>SIN</c>. Nine digits, no formatting. Sensitive: never log it.</summary>
        public string? Sin { get; init; }

        /// <summary>Gets <c>PHN</c>. Ten digits when seen. Sensitive: never log it.</summary>
        public string? Phn { get; init; }

        /// <summary>Gets <c>Person ID ICM</c>. The person number ICM's screens show.</summary>
        public string? PersonId { get; init; }

        /// <summary>Gets <c>Person ID MIS</c>. The legacy MIS person id — the same value as <c>ICMContact</c>'s <c>Integration Id</c>.</summary>
        public string? MisPersonId { get; init; }

        /// <summary>Gets <c>BCeID User Name</c>. The BCeID user name on file. The only place a BCeID has been found in ICM's REST surface (MEASURED 2026-09-24); <c>ICMContact</c> has no such field.</summary>
        public string? BceidUserName { get; init; }

        /// <summary>Gets <c>Home Phone</c>.</summary>
        public string? HomePhone { get; init; }

        /// <summary>Gets <c>Street Address</c>. <c>NFA</c> (no fixed address) when seen.</summary>
        public string? StreetAddress { get; init; }

        /// <summary>Gets <c>City</c>.</summary>
        public string? City { get; init; }

        /// <summary>Gets <c>Country</c>.</summary>
        public string? Country { get; init; }

        /// <summary>Gets <c>Primary Address</c>. The address as one line.</summary>
        public string? PrimaryAddress { get; init; }

        /// <summary>Gets <c>Citizen</c>. ICM's own text, e.g. <c>Yes</c>.</summary>
        public string? Citizen { get; init; }

        /// <summary>Gets <c>Indigenous</c>. ICM's own text, e.g. <c>TBD</c>.</summary>
        public string? Indigenous { get; init; }

        /// <summary>Gets <c>Subject</c>. Whether the person is the subject of the case.</summary>
        public bool? IsSubject { get; init; }

        /// <summary>Gets <c>Subject Child</c>.</summary>
        public bool? IsSubjectChild { get; init; }

        /// <summary>Gets <c>Parent_Caregiver</c>.</summary>
        public bool? IsParentOrCaregiver { get; init; }

        /// <summary>Gets <c>Deceased</c>.</summary>
        public bool? IsDeceased { get; init; }

        /// <summary>Gets <c>Potential Duplicate</c>. ICM's own note that the person may exist twice.</summary>
        public bool? IsPotentialDuplicate { get; init; }

        /// <summary>Gets <c>Integration State</c>. E.g. <c>Synced</c>.</summary>
        public string? IntegrationState { get; init; }

        /// <summary>Gets <c>Created Date</c>, a zone-less date and time exactly as written. When the contact record was created; zone-less as sent.</summary>
        public DateTime? CreatedDate { get; init; }

        /// <summary>Gets <c>Updated Date</c>, a zone-less date and time exactly as written. When it was last updated; zone-less as sent.</summary>
        public DateTime? UpdatedDate { get; init; }

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
