namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An ICM service request, as the rest of the application sees it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read-only: this is what came back from ICM. Build a
    /// <see cref="ServiceRequestInput"/> to write. The split is the point of the model —
    /// the sixteen fields ICM calculates and refuses to accept on a write are simply not
    /// settable here, so passing one back is a compile error rather than a silently
    /// ignored field.
    /// </para>
    /// <para>
    /// Properties are typed from the spec's <c>x-siebel-datatype</c>: flags are
    /// <see cref="bool"/>, and the three date types keep their distinctions — an instant
    /// is a <see cref="DateTimeOffset"/>, a zone-less date and time is a
    /// <see cref="DateTime"/>, a plain date is a <see cref="DateOnly"/>. Row ids and
    /// phone numbers stay strings, which is what they are.
    /// </para>
    /// <para>
    /// Dates are read as ISO 8601, the format Siebel documents. One that arrives in some
    /// other shape is <b>not</b> dropped: it comes through in <see cref="UnparsedValues"/>
    /// with the raw text intact.
    /// </para>
    /// </remarks>
    public class ServiceRequest
    {
        /// <summary>Gets the ICM row id, when the record came from a write.</summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>Address Comments</c> (max 255). Read-only in ICM.</summary>
        public string? AddressComments { get; init; }

        /// <summary>Gets <c>ICMCPU Aborginal</c> (max 255).</summary>
        public string? ICMCPUAborginal { get; init; }

        /// <summary>Gets <c>Call Date</c> (a date and time, with no time zone).</summary>
        public DateTime? CallDate { get; init; }

        /// <summary>Gets <c>CP Caller Address</c> (max 250).</summary>
        public string? CPCallerAddress { get; init; }

        /// <summary>Gets <c>CP Caller Email</c> (max 350).</summary>
        public string? CPCallerEmail { get; init; }

        /// <summary>Gets <c>CP Caller Name</c> (max 150).</summary>
        public string? CPCallerName { get; init; }

        /// <summary>Gets <c>CP Caller Phone</c> (a phone number; max 40).</summary>
        public string? CPCallerPhone { get; init; }

        /// <summary>Gets <c>Contact Cell #</c> (a phone number; max 40).</summary>
        public string? ContactCellNumber { get; init; }

        /// <summary>Gets <c>ICM Created By Office</c> (max 100). Read-only in ICM.</summary>
        public string? ICMCreatedByOffice { get; init; }

        /// <summary>Gets <c>Contact Given Name</c> (max 255). Read-only in ICM.</summary>
        public string? ContactGivenName { get; init; }

        /// <summary>Gets <c>Contact Home Phone</c> (a phone number; max 40). Read-only in ICM.</summary>
        public string? ContactHomePhone { get; init; }

        /// <summary>Gets <c>KKCFS Flag</c>.</summary>
        public bool? KKCFSFlag { get; init; }

        /// <summary>Gets <c>Case Local Office</c> (max 255). Read-only in ICM.</summary>
        public string? CaseLocalOffice { get; init; }

        /// <summary>Gets <c>Memo</c> (max 255).</summary>
        public string? Memo { get; init; }

        /// <summary>Gets <c>CP Nature of Call</c> (max 30).</summary>
        public string? CPNatureOfCall { get; init; }

        /// <summary>Gets <c>CP PCC Analysis</c> (max 2000).</summary>
        public string? CPPCCAnalysis { get; init; }

        /// <summary>Gets <c>CP Caller Pref Contact Method</c> (max 30).</summary>
        public string? CPCallerPrefContactMethod { get; init; }

        /// <summary>Gets <c>Restricted Flag</c>.</summary>
        public bool? RestrictedFlag { get; init; }

        /// <summary>Gets <c>CP Caller Type</c> (max 30).</summary>
        public string? CPCallerType { get; init; }

        /// <summary>Gets <c>Primary Contact Id</c> (an ICM row id; max 15).</summary>
        public string? PrimaryContactId { get; init; }

        /// <summary>Gets <c>ICM Stage</c> (max 50).</summary>
        public string? ICMStage { get; init; }

        /// <summary>Gets <c>Primary Organization Id</c> (an ICM row id; max 15).</summary>
        public string? PrimaryOrganizationId { get; init; }

        /// <summary>Gets <c>ICM CGA Due Diligence Decision</c> (max 50).</summary>
        public string? ICMCGADueDiligenceDecision { get; init; }

        /// <summary>Gets <c>ICM CGA Resolution Decision Date</c> (a date).</summary>
        public DateOnly? ICMCGAResolutionDecisionDate { get; init; }

        /// <summary>Gets <c>Primary Organization Name</c> (max 100). Read-only in ICM.</summary>
        public string? PrimaryOrganizationName { get; init; }

        /// <summary>Gets <c>ICM CGA Application Received Flag</c>.</summary>
        public bool? ICMCGAApplicationReceivedFlag { get; init; }

        /// <summary>Gets <c>CP Outcome</c> (max 3500).</summary>
        public string? CPOutcome { get; init; }

        /// <summary>Gets <c>Created</c> (an instant, UTC; required on a complete record). Read-only in ICM.</summary>
        public DateTimeOffset? Created { get; init; }

        /// <summary>Gets <c>Created By</c> (an ICM row id; max 15; required on a complete record). Read-only in ICM.</summary>
        public string? CreatedBy { get; init; }

        /// <summary>Gets <c>Updated</c> (an instant, UTC; required on a complete record). Read-only in ICM.</summary>
        public DateTimeOffset? Updated { get; init; }

        /// <summary>Gets <c>Updated By Name</c> (max 50). Read-only in ICM.</summary>
        public string? UpdatedByName { get; init; }

        /// <summary>Gets <c>Updated By</c> (an ICM row id; max 15; required on a complete record). Read-only in ICM.</summary>
        public string? UpdatedBy { get; init; }

        /// <summary>Gets <c>SR KP Address Calc</c> (max 255). Read-only in ICM.</summary>
        public string? SRKPAddressCalc { get; init; }

        /// <summary>Gets <c>Close Date Calc</c> (an instant, UTC). Read-only in ICM.</summary>
        public DateTimeOffset? CloseDateCalc { get; init; }

        /// <summary>Gets <c>Comm Method</c> (max 30; required on a complete record).</summary>
        public string? CommMethod { get; init; }

        /// <summary>Gets <c>Contact Last Name</c> (max 50).</summary>
        public string? ContactLastName { get; init; }

        /// <summary>Gets <c>Created By Name</c> (max 50). Read-only in ICM.</summary>
        public string? CreatedByName { get; init; }

        /// <summary>Gets <c>Integration Id</c> (max 30).</summary>
        public string? IntegrationId { get; init; }

        /// <summary>Gets <c>CP Caller Method</c> (max 30).</summary>
        public string? CPCallerMethod { get; init; }

        /// <summary>Gets <c>Assigned To Id</c> (an ICM row id; max 15). Read-only in ICM.</summary>
        public string? AssignedToId { get; init; }

        /// <summary>Gets <c>Assigned To</c> (max 255). Read-only in ICM.</summary>
        public string? AssignedTo { get; init; }

        /// <summary>Gets <c>Priority</c> (max 30; required on a complete record).</summary>
        public string? Priority { get; init; }

        /// <summary>Gets <c>Resolution Code</c> (max 30).</summary>
        public string? ResolutionCode { get; init; }

        /// <summary>Gets <c>SR Number</c> (max 64).</summary>
        public string? SRNumber { get; init; }

        /// <summary>Gets <c>SR Type</c> (max 30; required on a complete record).</summary>
        public string? SRType { get; init; }

        /// <summary>Gets <c>SR Sub Type</c> (max 30).</summary>
        public string? SRSubType { get; init; }

        /// <summary>Gets <c>SR Sub Sub Type</c> (max 30).</summary>
        public string? SRSubSubType { get; init; }

        /// <summary>Gets <c>Status</c> (max 30; required on a complete record).</summary>
        public string? Status { get; init; }

        /// <summary>Gets <c>Service Office</c> (max 100; required on a complete record).</summary>
        public string? ServiceOffice { get; init; }

        /// <summary>Gets <c>ICM BCSC DID</c> (max 255).</summary>
        public string? ICMBCSCDID { get; init; }

        /// <summary>Gets the child links ICM returned, when they were asked for.</summary>
        public IReadOnlyList<ServiceRequestLink> Links { get; init; } = [];

        /// <summary>
        /// Gets any field whose raw value could not be converted to the type the spec
        /// declares, keyed by its ICM field name.
        /// </summary>
        /// <remarks>
        /// Normally empty. A value in here means ICM sent a date that is not ISO 8601,
        /// and the corresponding typed property is null — the data is not lost, but
        /// something disagrees with Siebel's documented format and wants looking at before
        /// a format is added on an assumption. Worth asserting empty in an integration test
        /// against SIT.
        /// </remarks>
        public IReadOnlyDictionary<string, string> UnparsedValues { get; init; } =
            new Dictionary<string, string>();
    }
}
