namespace Icm.Api.Models
{
    using System;

    /// <summary>
    /// The fields of a service request ICM should accept on a create or an update.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Untested.</b> No write has been made against a real ICM. Which fields are
    /// writable is taken from the OpenAPI document's read-only flags, carried across the
    /// rename map onto the names ICM actually uses — so it is one inference removed from
    /// evidence. A rejected field will fail loudly.
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
    }
}
