namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>An ICM activity — a task, system task or notification, profiled to a service request, case, incident or memo.</summary>
    /// <remarks>
    /// <para>
    /// The events on a bus pass service request are rows of this component: the steps of
    /// the activity plan the workflow applies, each with an <see cref="ActionCode"/> (the
    /// step's name), an <see cref="ActionBy"/> of <c>Staff</c> or <c>System</c>, a
    /// <see cref="Sequence"/> (<c>01</c>, <c>01-01</c>, …) and, once done, a
    /// <see cref="ResultCode"/>. <see cref="ActivityPlanId"/> ties a step to its plan
    /// instance (an <see cref="ActivityPlan"/>); an activity created directly has none.
    /// </para>
    /// <para>
    /// Field names are the describe document's, which are the live ones. Read-only: every
    /// property is <c>init</c>; a create takes an <see cref="ActivityInput"/>.
    /// </para>
    /// </remarks>
    public class Activity
    {
        /// <summary>Gets <c>Id</c>. The row id.</summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>Activity SR Id</c>. The service request the activity is profiled to.</summary>
        public string? ServiceRequestId { get; init; }

        /// <summary>Gets <c>SR Number</c>.</summary>
        public string? ServiceRequestNumber { get; init; }

        /// <summary>Gets <c>SR Type</c>.</summary>
        public string? ServiceRequestType { get; init; }

        /// <summary>Gets <c>SR Sub Type</c>.</summary>
        public string? ServiceRequestSubType { get; init; }

        /// <summary>Gets <c>Case Id</c>.</summary>
        public string? CaseId { get; init; }

        /// <summary>Gets <c>Case Number</c>.</summary>
        public string? CaseNumber { get; init; }

        /// <summary>Gets <c>Incident Id</c>.</summary>
        public string? IncidentId { get; init; }

        /// <summary>Gets <c>ICM Memo Id</c>.</summary>
        public string? MemoId { get; init; }

        /// <summary>Gets <c>Activity Template Id</c>: the plan instance the step belongs to, or null for an activity created on its own.</summary>
        public string? ActivityPlanId { get; init; }

        /// <summary>Gets <c>Plan Name</c>, e.g. <c>Bus Pass - New Intake</c>.</summary>
        public string? PlanName { get; init; }

        /// <summary>Gets <c>Sequence</c>, the step's position in its plan, e.g. <c>01-01-01</c>.</summary>
        public string? Sequence { get; init; }

        /// <summary>Gets <c>Type</c>, e.g. <c>Task</c>, <c>System Task</c>.</summary>
        public string? Type { get; init; }

        /// <summary>Gets <c>ICM Type</c>.</summary>
        public string? IcmType { get; init; }

        /// <summary>Gets <c>ICM Sub Type</c>.</summary>
        public string? IcmSubType { get; init; }

        /// <summary>Gets <c>Category</c>, e.g. <c>General</c>.</summary>
        public string? Category { get; init; }

        /// <summary>Gets <c>Class</c>.</summary>
        public string? Class { get; init; }

        /// <summary>Gets <c>Action By</c>: <c>Staff</c> or <c>System</c>.</summary>
        public string? ActionBy { get; init; }

        /// <summary>Gets <c>Action Code</c>, the step's name, e.g. <c>Validate Address and Promote Prospect</c>.</summary>
        public string? ActionCode { get; init; }

        /// <summary>Gets <c>Result Code</c>, the step's outcome, e.g. <c>Complete</c>, <c>Case Created</c>.</summary>
        public string? ResultCode { get; init; }

        /// <summary>Gets <c>Description</c>.</summary>
        public string? Description { get; init; }

        /// <summary>Gets <c>Display</c>, e.g. <c>Calendar and Activities</c>.</summary>
        public string? Display { get; init; }

        /// <summary>Gets <c>Status</c>: <c>Open</c>, <c>Closed</c>, <c>Cancelled</c>.</summary>
        public string? Status { get; init; }

        /// <summary>Gets <c>Done Flag</c>.</summary>
        public bool? IsDone { get; init; }

        /// <summary>Gets <c>Done</c>, a zone-less date and time as written. When it was completed.</summary>
        public DateTime? Done { get; init; }

        /// <summary>Gets <c>Due</c>, a zone-less date and time as written.</summary>
        public DateTime? Due { get; init; }

        /// <summary>Gets <c>Planned</c>, a zone-less date and time as written.</summary>
        public DateTime? Planned { get; init; }

        /// <summary>Gets <c>Planned Completion</c>, a zone-less date and time as written.</summary>
        public DateTime? PlannedCompletion { get; init; }

        /// <summary>Gets <c>Duration Minutes</c>, kept as text.</summary>
        public string? DurationMinutes { get; init; }

        /// <summary>Gets <c>Priority</c>, e.g. <c>3-Standard</c>.</summary>
        public string? Priority { get; init; }

        /// <summary>Gets <c>Owned By</c>, the owner's login.</summary>
        public string? OwnedBy { get; init; }

        /// <summary>Gets <c>Owned By Id</c>.</summary>
        public string? OwnedById { get; init; }

        /// <summary>Gets <c>Primary Owned By</c>, the primary owner's login.</summary>
        public string? PrimaryOwnedBy { get; init; }

        /// <summary>Gets <c>Primary Owner Id</c>; <c>No Match Row Id</c> when unassigned.</summary>
        public string? PrimaryOwnerId { get; init; }

        /// <summary>Gets <c>Primary Contact Id</c>.</summary>
        public string? PrimaryContactId { get; init; }

        /// <summary>Gets <c>Contact First Name</c>.</summary>
        public string? ContactFirstName { get; init; }

        /// <summary>Gets <c>Contact Last Name</c>.</summary>
        public string? ContactLastName { get; init; }

        /// <summary>Gets <c>Service Office</c>.</summary>
        public string? ServiceOffice { get; init; }

        /// <summary>Gets <c>ICM Service Region</c>.</summary>
        public string? ServiceRegion { get; init; }

        /// <summary>Gets <c>Ministry</c>.</summary>
        public string? Ministry { get; init; }

        /// <summary>Gets <c>Ministry Id</c>.</summary>
        public string? MinistryId { get; init; }

        /// <summary>Gets <c>ICM Created By Office</c>.</summary>
        public string? CreatedByOffice { get; init; }

        /// <summary>Gets <c>Restricted Flag</c>.</summary>
        public bool? IsRestricted { get; init; }

        /// <summary>Gets <c>Shared Flag</c>.</summary>
        public bool? IsShared { get; init; }

        /// <summary>Gets <c>Repeating</c>.</summary>
        public bool? IsRepeating { get; init; }

        /// <summary>Gets <c>Alarm</c>.</summary>
        public bool? HasAlarm { get; init; }

        /// <summary>Gets <c>Overdue</c>.</summary>
        public bool? IsOverdue { get; init; }

        /// <summary>Gets <c>Show In Contact Profile</c>.</summary>
        public bool? ShowInContactProfile { get; init; }

        /// <summary>Gets <c>Created</c>, a zone-less date and time as written (the document says UTC; the value is the SR's wall clock).</summary>
        public DateTime? Created { get; init; }

        /// <summary>Gets <c>Created By</c>, the creating user's row id.</summary>
        public string? CreatedById { get; init; }

        /// <summary>Gets <c>Created By Name</c>, the creating user's login.</summary>
        public string? CreatedBy { get; init; }

        /// <summary>Gets <c>Updated</c>, a zone-less date and time as written.</summary>
        public DateTime? Updated { get; init; }

        /// <summary>Gets <c>Updated By</c>, the updating user's row id.</summary>
        public string? UpdatedById { get; init; }

        /// <summary>Gets <c>Updated By Name</c>.</summary>
        public string? UpdatedBy { get; init; }

        /// <summary>Gets anything ICM sent that is not modelled above, as raw JSON.</summary>
        public IReadOnlyDictionary<string, JsonElement> AdditionalFields { get; init; } =
            new Dictionary<string, JsonElement>();

        /// <summary>Gets the values that arrived in a shape the client could not read, keyed by ICM field name.</summary>
        public IReadOnlyDictionary<string, string> UnparsedValues { get; init; } =
            new Dictionary<string, string>();
    }
}
