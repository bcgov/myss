namespace Icm.Api.Models
{
    using System;

    /// <summary>The fields of an activity ICM should accept on a create.</summary>
    /// <remarks>
    /// <para>
    /// The shape of ICM's REST API guidance ("GET-PUT-POST Activities", ADO 8646): a PUT
    /// keyed by the placeholder <c>Id</c> <c>NULL</c>, profiled to a service request by
    /// <see cref="ServiceRequestId"/> (or to a case, incident or memo by their ids).
    /// MEASURED SIT2 2026-09-29: activity <c>1-53MU3Y4</c> created on the directly filed
    /// bus pass SR <c>1-53MU3X7</c>, with the field values of the workflow's step 01.
    /// </para>
    /// <para>
    /// The document requires <c>Type</c>, <c>Action By</c>, <c>Category</c> and
    /// <c>Display</c>; the library insists on the first two and defaults the other two to
    /// the values every bus pass step carries. Only the properties that are set are sent.
    /// </para>
    /// <para>
    /// <b>There is no <c>Id</c>.</b> The create is a PUT to the collection, which Siebel
    /// treats as an upsert: a caller-supplied row id would update an existing activity.
    /// The placeholder <c>NULL</c> is always sent.
    /// </para>
    /// </remarks>
    public class ActivityInput
    {
        /// <summary>Gets or sets <c>Activity SR Id</c>: the service request to profile the activity to.</summary>
        public string? ServiceRequestId { get; set; }

        /// <summary>Gets or sets <c>Case Id</c>: the case to profile the activity to.</summary>
        public string? CaseId { get; set; }

        /// <summary>Gets or sets <c>Incident Id</c>: the incident to profile the activity to.</summary>
        public string? IncidentId { get; set; }

        /// <summary>Gets or sets <c>ICM Memo Id</c>: the memo to profile the activity to.</summary>
        public string? MemoId { get; set; }

        /// <summary>Gets or sets <c>Activity Template Id</c>: the plan instance the step belongs to, when there is one.</summary>
        public string? ActivityPlanId { get; set; }

        /// <summary>Gets or sets <c>Type</c>, e.g. <c>Task</c>. Required.</summary>
        public string? Type { get; set; }

        /// <summary>Gets or sets <c>ICM Sub Type</c>.</summary>
        public string? IcmSubType { get; set; }

        /// <summary>Gets or sets <c>Category</c>; null sends <c>General</c>.</summary>
        public string? Category { get; set; }

        /// <summary>Gets or sets <c>Action By</c>: <c>Staff</c> or <c>System</c>. Required.</summary>
        public string? ActionBy { get; set; }

        /// <summary>Gets or sets <c>Action Code</c>, the step's name.</summary>
        public string? ActionCode { get; set; }

        /// <summary>Gets or sets <c>Result Code</c>.</summary>
        public string? ResultCode { get; set; }

        /// <summary>Gets or sets <c>Description</c> (max 250).</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets <c>Display</c>; null sends <c>Calendar and Activities</c>.</summary>
        public string? Display { get; set; }

        /// <summary>Gets or sets <c>Status</c>, e.g. <c>Open</c>.</summary>
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Priority</c>, e.g. <c>3-Standard</c>.</summary>
        public string? Priority { get; set; }

        /// <summary>Gets or sets <c>Sequence</c>, e.g. <c>01</c>.</summary>
        public string? Sequence { get; set; }

        /// <summary>Gets or sets <c>Planned</c>, written <c>MM/dd/yyyy HH:mm:ss</c> as a wall-clock time.</summary>
        public DateTime? Planned { get; set; }

        /// <summary>Gets or sets <c>Due</c>, written <c>MM/dd/yyyy HH:mm:ss</c> as a wall-clock time.</summary>
        public DateTime? Due { get; set; }

        /// <summary>Gets or sets <c>Done Flag</c>.</summary>
        public bool? IsDone { get; set; }

        /// <summary>Gets or sets <c>Primary Owned By</c>, the login to assign the activity to.</summary>
        public string? PrimaryOwnedBy { get; set; }

        /// <summary>Gets or sets <c>Service Office</c>.</summary>
        public string? ServiceOffice { get; set; }

        /// <summary>Gets or sets <c>ICM Created By Office</c>.</summary>
        public string? CreatedByOffice { get; set; }

        /// <summary>Gets or sets <c>Ministry</c>.</summary>
        public string? Ministry { get; set; }
    }
}
