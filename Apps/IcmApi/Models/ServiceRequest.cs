namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// An ICM service request, as the rest of the application sees it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read-only: this is what came back from ICM. Build a
    /// <see cref="ServiceRequestInput"/> to write. The fields ICM calculates are not
    /// settable there, so passing one back is a compile error rather than a field ICM
    /// silently drops.
    /// </para>
    /// <para>
    /// Types come from the values ICM actually sends: <c>Y</c>/<c>N</c> flags are
    /// <see cref="bool"/>, and the four date fields are <see cref="DateTime"/> with
    /// <see cref="DateTimeKind.Unspecified"/> — the wire carries no offset, and the value
    /// matches what the Siebel UI displays character for character, so no zone is
    /// invented for them.
    /// </para>
    /// <para>
    /// Anything ICM sends that this type has no property for arrives in
    /// <see cref="AdditionalFields"/> as raw JSON, and a date in an unrecognised shape
    /// arrives in <see cref="UnparsedValues"/>. Neither is ever dropped.
    /// </para>
    /// </remarks>
    public class ServiceRequest
    {
        /// <summary>Gets <c>Address</c> (max 255). Read-only in ICM.</summary>
        public string? Address { get; init; }

        /// <summary>Gets <c>Address Comments</c> (max 255; never seen with a value). Read-only in ICM.</summary>
        public string? AddressComments { get; init; }

        /// <summary>Gets <c>Are Any Of The Family Members Indigenous</c> (max 255).</summary>
        public string? AreAnyOfTheFamilyMembersIndigenous { get; init; }

        /// <summary>Gets <c>Assigned To</c> (max 255). Read-only in ICM.</summary>
        public string? AssignedTo { get; init; }

        /// <summary>Gets <c>Assigned To Id</c> (max 15). Read-only in ICM.</summary>
        public string? AssignedToId { get; init; }

        /// <summary>Gets <c>Call Date</c> (a date and time, with no time zone).</summary>
        public DateTime? CallDate { get; init; }

        /// <summary>Gets <c>Caller Address</c> (max 250; never seen with a value).</summary>
        public string? CallerAddress { get; init; }

        /// <summary>Gets <c>Caller Email</c> (max 350; never seen with a value).</summary>
        public string? CallerEmail { get; init; }

        /// <summary>Gets <c>Caller Name</c> (max 150; never seen with a value).</summary>
        public string? CallerName { get; init; }

        /// <summary>Gets <c>Caller Phone</c> (max 40; never seen with a value).</summary>
        public string? CallerPhone { get; init; }

        /// <summary>Gets <c>Case Local Office</c> (max 255). Read-only in ICM.</summary>
        public string? CaseLocalOffice { get; init; }

        /// <summary>Gets <c>Cell Phone</c> (max 40).</summary>
        public string? CellPhone { get; init; }

        /// <summary>Gets <c>Closed Date</c> (a date and time, with no time zone). Read-only in ICM.</summary>
        public DateTime? ClosedDate { get; init; }

        /// <summary>Gets <c>Comm Method</c> (max 30).</summary>
        public string? CommMethod { get; init; }

        /// <summary>Gets <c>Created By</c> (max 50). Read-only in ICM.</summary>
        public string? CreatedBy { get; init; }

        /// <summary>Gets <c>Created By Id</c> (max 15). Read-only in ICM.</summary>
        public string? CreatedById { get; init; }

        /// <summary>Gets <c>Created By Office</c> (max 100). Read-only in ICM.</summary>
        public string? CreatedByOffice { get; init; }

        /// <summary>Gets <c>Created Date</c> (a date and time, with no time zone). Read-only in ICM.</summary>
        public DateTime? CreatedDate { get; init; }

        /// <summary>Gets <c>Given Names</c> (max 255). Read-only in ICM.</summary>
        public string? GivenNames { get; init; }

        /// <summary>Gets <c>Home Phone</c> (max 40; never seen with a value). Read-only in ICM.</summary>
        public string? HomePhone { get; init; }

        /// <summary>Gets <c>ICM BCSC DID</c> (max 255; never seen with a value).</summary>
        public string? ICMBCSCDID { get; init; }

        /// <summary>Gets <c>ICM CGA Application Received Flag</c> (a Y/N flag; never seen with a value).</summary>
        public bool? ICMCGAApplicationReceivedFlag { get; init; }

        /// <summary>Gets <c>ICM CGA Due Diligence Decision</c> (max 50; never seen with a value).</summary>
        public string? ICMCGADueDiligenceDecision { get; init; }

        /// <summary>Gets <c>ICM CGA Resolution Decision Date</c> (a date; never seen with a value).</summary>
        public DateOnly? ICMCGAResolutionDecisionDate { get; init; }

        /// <summary>Gets <c>ICM Stage</c> (max 50; never seen with a value).</summary>
        public string? ICMStage { get; init; }

        /// <summary>Gets <c>Id</c> (not in the OpenAPI document). Read-only in ICM.</summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>Integration Id</c> (max 30; never seen with a value).</summary>
        public string? IntegrationId { get; init; }

        /// <summary>Gets <c>Kkcfs</c> (a Y/N flag).</summary>
        public bool? Kkcfs { get; init; }

        /// <summary>Gets <c>Last Name</c> (max 50).</summary>
        public string? LastName { get; init; }

        /// <summary>Gets <c>Memo</c> (max 255; never seen with a value).</summary>
        public string? Memo { get; init; }

        /// <summary>Gets <c>Method</c> (max 30; never seen with a value).</summary>
        public string? Method { get; init; }

        /// <summary>Gets <c>Nature Of Call</c> (max 30; never seen with a value).</summary>
        public string? NatureOfCall { get; init; }

        /// <summary>Gets <c>Pcc Summary</c> (max 2000; never seen with a value).</summary>
        public string? PccSummary { get; init; }

        /// <summary>Gets <c>Preferred Contact Method</c> (max 30; never seen with a value).</summary>
        public string? PreferredContactMethod { get; init; }

        /// <summary>Gets <c>Primary Contact Id</c> (max 15).</summary>
        public string? PrimaryContactId { get; init; }

        /// <summary>Gets <c>Primary Organization Id</c> (max 15).</summary>
        public string? PrimaryOrganizationId { get; init; }

        /// <summary>Gets <c>Primary Organization Name</c> (max 100). Read-only in ICM.</summary>
        public string? PrimaryOrganizationName { get; init; }

        /// <summary>Gets <c>Priority</c> (max 30).</summary>
        public string? Priority { get; init; }

        /// <summary>Gets <c>Resolution</c> (max 3500).</summary>
        public string? Resolution { get; init; }

        /// <summary>Gets <c>Restricted Flag</c> (a Y/N flag).</summary>
        public bool? RestrictedFlag { get; init; }

        /// <summary>Gets <c>Row Id</c> (not in the OpenAPI document). Read-only in ICM.</summary>
        public string? RowId { get; init; }

        /// <summary>Gets <c>SR Sub Sub Type</c> (max 30).</summary>
        public string? SRSubSubType { get; init; }

        /// <summary>Gets <c>SR Sub Type</c> (max 30).</summary>
        public string? SRSubType { get; init; }

        /// <summary>Gets <c>Service Office</c> (max 100).</summary>
        public string? ServiceOffice { get; init; }

        /// <summary>Gets <c>Service Request Number</c> (max 64).</summary>
        public string? ServiceRequestNumber { get; init; }

        /// <summary>Gets <c>Status</c> (max 30).</summary>
        public string? Status { get; init; }

        /// <summary>Gets <c>Type</c> (max 30).</summary>
        public string? Type { get; init; }

        /// <summary>Gets <c>Type Of Caller</c> (max 30; never seen with a value).</summary>
        public string? TypeOfCaller { get; init; }

        /// <summary>Gets <c>Updated By</c> (max 50). Read-only in ICM.</summary>
        public string? UpdatedBy { get; init; }

        /// <summary>Gets <c>Updated By Id</c> (max 15). Read-only in ICM.</summary>
        public string? UpdatedById { get; init; }

        /// <summary>Gets <c>Updated Date</c> (a date and time, with no time zone). Read-only in ICM.</summary>
        public DateTime? UpdatedDate { get; init; }

        /// <summary>Gets the self and child links ICM returned.</summary>
        public IReadOnlyList<ServiceRequestLink> Links { get; init; } = [];

        /// <summary>
        /// Gets the raw JSON of every field ICM sent that this type does not model,
        /// keyed by its ICM field name.
        /// </summary>
        /// <remarks>
        /// Normally empty. A value here means ICM sent something new — a field added
        /// upstream, or a record shape this client has not met. It is raw
        /// <see cref="JsonElement"/> rather than a string so the original type survives
        /// review: call <see cref="JsonElement.GetRawText"/> to print it.
        /// </remarks>
        public IReadOnlyDictionary<string, JsonElement> AdditionalFields { get; init; } =
            new Dictionary<string, JsonElement>();

        /// <summary>
        /// Gets any field whose raw value could not be converted to the type it usually
        /// carries, keyed by its ICM field name.
        /// </summary>
        /// <remarks>
        /// Normally empty. A value here means a date arrived in a shape
        /// <c>SiebelDate</c> does not recognise and the typed property is null — the data
        /// is not lost, but the format list needs a case adding. Worth asserting empty in
        /// an integration test against SIT.
        /// </remarks>
        public IReadOnlyDictionary<string, string> UnparsedValues { get; init; } =
            new Dictionary<string, string>();
    }
}
