namespace Icm.Api.Models
{
    using System;

    /// <summary>
    /// The fields of a service request that ICM will accept on a create or an update.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sixteen fields ICM marks read-only are absent by design — an update cannot
    /// express something ICM would ignore.
    /// </para>
    /// <para>
    /// <b>Only the properties that are set are sent.</b> An update carrying one property
    /// changes one field; it does not blank the rest. That also means a record read back
    /// from ICM cannot be round-tripped by copying it wholesale — decide what is changing
    /// and set only that.
    /// </para>
    /// <para>
    /// Dates are written as ISO 8601, which is the format Siebel documents.
    /// </para>
    /// </remarks>
    public class ServiceRequestInput
    {
        /// <summary>Gets or sets <c>ICMCPU Aborginal</c> (max 255).</summary>
        public string? ICMCPUAborginal { get; set; }

        /// <summary>Gets or sets <c>Call Date</c> (a date and time, with no time zone).</summary>
        public DateTime? CallDate { get; set; }

        /// <summary>Gets or sets <c>CP Caller Address</c> (max 250).</summary>
        public string? CPCallerAddress { get; set; }

        /// <summary>Gets or sets <c>CP Caller Email</c> (max 350).</summary>
        public string? CPCallerEmail { get; set; }

        /// <summary>Gets or sets <c>CP Caller Name</c> (max 150).</summary>
        public string? CPCallerName { get; set; }

        /// <summary>Gets or sets <c>CP Caller Phone</c> (a phone number; max 40).</summary>
        public string? CPCallerPhone { get; set; }

        /// <summary>Gets or sets <c>Contact Cell #</c> (a phone number; max 40).</summary>
        public string? ContactCellNumber { get; set; }

        /// <summary>Gets or sets <c>KKCFS Flag</c>.</summary>
        public bool? KKCFSFlag { get; set; }

        /// <summary>Gets or sets <c>Memo</c> (max 255).</summary>
        public string? Memo { get; set; }

        /// <summary>Gets or sets <c>CP Nature of Call</c> (max 30).</summary>
        public string? CPNatureOfCall { get; set; }

        /// <summary>Gets or sets <c>CP PCC Analysis</c> (max 2000).</summary>
        public string? CPPCCAnalysis { get; set; }

        /// <summary>Gets or sets <c>CP Caller Pref Contact Method</c> (max 30).</summary>
        public string? CPCallerPrefContactMethod { get; set; }

        /// <summary>Gets or sets <c>Restricted Flag</c>.</summary>
        public bool? RestrictedFlag { get; set; }

        /// <summary>Gets or sets <c>CP Caller Type</c> (max 30).</summary>
        public string? CPCallerType { get; set; }

        /// <summary>Gets or sets <c>Primary Contact Id</c> (an ICM row id; max 15).</summary>
        public string? PrimaryContactId { get; set; }

        /// <summary>Gets or sets <c>ICM Stage</c> (max 50).</summary>
        public string? ICMStage { get; set; }

        /// <summary>Gets or sets <c>Primary Organization Id</c> (an ICM row id; max 15).</summary>
        public string? PrimaryOrganizationId { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Due Diligence Decision</c> (max 50).</summary>
        public string? ICMCGADueDiligenceDecision { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Resolution Decision Date</c> (a date).</summary>
        public DateOnly? ICMCGAResolutionDecisionDate { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Application Received Flag</c>.</summary>
        public bool? ICMCGAApplicationReceivedFlag { get; set; }

        /// <summary>Gets or sets <c>CP Outcome</c> (max 3500).</summary>
        public string? CPOutcome { get; set; }

        /// <summary>Gets or sets <c>Comm Method</c> (max 30; required on a complete record).</summary>
        public string? CommMethod { get; set; }

        /// <summary>Gets or sets <c>Contact Last Name</c> (max 50).</summary>
        public string? ContactLastName { get; set; }

        /// <summary>Gets or sets <c>Integration Id</c> (max 30).</summary>
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets <c>CP Caller Method</c> (max 30).</summary>
        public string? CPCallerMethod { get; set; }

        /// <summary>Gets or sets <c>Priority</c> (max 30; required on a complete record).</summary>
        public string? Priority { get; set; }

        /// <summary>Gets or sets <c>Resolution Code</c> (max 30).</summary>
        public string? ResolutionCode { get; set; }

        /// <summary>Gets or sets <c>SR Number</c> (max 64).</summary>
        public string? SRNumber { get; set; }

        /// <summary>Gets or sets <c>SR Type</c> (max 30; required on a complete record).</summary>
        public string? SRType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Type</c> (max 30).</summary>
        public string? SRSubType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Sub Type</c> (max 30).</summary>
        public string? SRSubSubType { get; set; }

        /// <summary>Gets or sets <c>Status</c> (max 30; required on a complete record).</summary>
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Service Office</c> (max 100; required on a complete record).</summary>
        public string? ServiceOffice { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c> (max 255).</summary>
        public string? ICMBCSCDID { get; set; }
    }
}
