namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The fields of a service request ICM should accept on a create or an update.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A create is MEASURED</b> (SIT2 2026-09-29: bus pass SRs <c>1-53MU3X7</c> and
    /// <c>1-53MU3XD</c>) for <see cref="Type"/>, <see cref="SRSubType"/>,
    /// <see cref="SRSubSubType"/>, <see cref="Status"/>, <see cref="Priority"/>,
    /// <see cref="CommMethod"/>, <see cref="ServiceOffice"/> and
    /// <see cref="PrimaryOrganizationId"/> (accepted, but the stored SR reads back with the
    /// trusted user's default organization). The rest of the writable set is taken from
    /// the document's read-only flags under the names ICM reads with, and an update has
    /// not been made live. A rejected field fails loudly.
    /// </para>
    /// <para>
    /// <b>Only the properties that are set are sent.</b> An update carrying one property
    /// changes one field; it does not blank the rest. That also means a record read back
    /// from ICM cannot be round-tripped by copying it wholesale — decide what is changing
    /// and set only that.
    /// </para>
    /// </remarks>
    public class ServiceRequestInput
    {
        /// <summary>
        /// Gets or sets <c>Id</c>, the row id — normally Siebel's to assign and left null.
        /// Set on a create only as the integration component's user key, the one ICM has
        /// been seen to use (2026-09-29); what Siebel does with a caller-supplied value is
        /// the experiment.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Are Any Of The Family Members Indigenous</c> (max 255).</summary>
        public string? AreAnyOfTheFamilyMembersIndigenous { get; set; }

        /// <summary>Gets or sets <c>Call Date</c> (a date and time, with no time zone).</summary>
        public DateTime? CallDate { get; set; }

        /// <summary>Gets or sets <c>Caller Address</c> (max 250; never seen with a value).</summary>
        public string? CallerAddress { get; set; }

        /// <summary>Gets or sets <c>Caller Email</c> (max 350; never seen with a value).</summary>
        public string? CallerEmail { get; set; }

        /// <summary>Gets or sets <c>Caller Name</c> (max 150; never seen with a value).</summary>
        public string? CallerName { get; set; }

        /// <summary>Gets or sets <c>Caller Phone</c> (max 40; never seen with a value).</summary>
        public string? CallerPhone { get; set; }

        /// <summary>Gets or sets <c>Cell Phone</c> (max 40).</summary>
        public string? CellPhone { get; set; }

        /// <summary>Gets or sets <c>Comm Method</c> (max 30).</summary>
        public string? CommMethod { get; set; }

        /// <summary>Gets or sets <c>ICM BCSC DID</c> (max 255; never seen with a value).</summary>
        public string? ICMBCSCDID { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Application Received Flag</c> (a Y/N flag; never seen with a value).</summary>
        public bool? ICMCGAApplicationReceivedFlag { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Due Diligence Decision</c> (max 50; never seen with a value).</summary>
        public string? ICMCGADueDiligenceDecision { get; set; }

        /// <summary>Gets or sets <c>ICM CGA Resolution Decision Date</c> (a date; never seen with a value).</summary>
        public DateOnly? ICMCGAResolutionDecisionDate { get; set; }

        /// <summary>Gets or sets <c>ICM Stage</c> (max 50; never seen with a value).</summary>
        public string? ICMStage { get; set; }

        /// <summary>Gets or sets <c>Integration Id</c> (max 30; never seen with a value).</summary>
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets <c>Kkcfs</c> (a Y/N flag).</summary>
        public bool? Kkcfs { get; set; }

        /// <summary>Gets or sets <c>Last Name</c> (max 50).</summary>
        public string? LastName { get; set; }

        /// <summary>Gets or sets <c>Memo</c> (max 255; never seen with a value).</summary>
        public string? Memo { get; set; }

        /// <summary>Gets or sets <c>Method</c> (max 30; never seen with a value).</summary>
        public string? Method { get; set; }

        /// <summary>Gets or sets <c>Nature Of Call</c> (max 30; never seen with a value).</summary>
        public string? NatureOfCall { get; set; }

        /// <summary>Gets or sets <c>Pcc Summary</c> (max 2000; never seen with a value).</summary>
        public string? PccSummary { get; set; }

        /// <summary>Gets or sets <c>Preferred Contact Method</c> (max 30; never seen with a value).</summary>
        public string? PreferredContactMethod { get; set; }

        /// <summary>Gets or sets <c>Primary Contact Id</c> (max 15).</summary>
        public string? PrimaryContactId { get; set; }

        /// <summary>Gets or sets <c>Primary Organization Id</c> (max 15).</summary>
        public string? PrimaryOrganizationId { get; set; }

        /// <summary>Gets or sets <c>Priority</c> (max 30).</summary>
        public string? Priority { get; set; }

        /// <summary>Gets or sets <c>Resolution</c> (max 3500).</summary>
        public string? Resolution { get; set; }

        /// <summary>Gets or sets <c>Restricted Flag</c> (a Y/N flag).</summary>
        public bool? RestrictedFlag { get; set; }

        /// <summary>Gets or sets <c>SR Sub Sub Type</c> (max 30).</summary>
        public string? SRSubSubType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Type</c> (max 30).</summary>
        public string? SRSubType { get; set; }

        /// <summary>Gets or sets <c>Service Office</c> (max 100).</summary>
        public string? ServiceOffice { get; set; }

        /// <summary>Gets or sets <c>Service Request Number</c> (max 64).</summary>
        public string? ServiceRequestNumber { get; set; }

        /// <summary>Gets or sets <c>Status</c> (max 30).</summary>
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Type</c> (max 30).</summary>
        public string? Type { get; set; }

        /// <summary>Gets or sets <c>Type Of Caller</c> (max 30; never seen with a value).</summary>
        public string? TypeOfCaller { get; set; }

        /// <summary>
        /// Gets or sets the prospects — applicant rows — to create together with the
        /// service request, nested in the same write. Null or empty sends none.
        /// </summary>
        /// <remarks>
        /// The one shape a prospect can be created in: a POST to the child path creates a
        /// new parent instead, and the standalone path has no usable user key (both
        /// MEASURED SIT2 2026-09-29). Each row needs its names; its
        /// <see cref="ProspectInput.ServiceRequestId"/> is left null, the enclosing record
        /// being the parent.
        /// </remarks>
        public IList<ProspectInput>? Prospects { get; set; }
    }
}
