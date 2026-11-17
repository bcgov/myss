namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A Siebel <c>Activities/Activities</c> record as it appears on the wire — the fields
    /// this library asks for, not the 84 the business component declares
    /// (<c>docs/integration/Activities_OpenApi.json</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The document's names are the live names</b> (MEASURED SIT2 2026-09-29: the 84
    /// document names plus <c>Id</c> come back from the standalone path; the child view
    /// under a service request adds <c>Activity Id</c>, <c>Row Id</c> and
    /// <c>Last Updated By Name</c>). An activity plan's steps are rows of this component,
    /// linked to their plan instance by <see cref="ActivityTemplateId"/> and to their
    /// service request by <see cref="ActivitySRId"/>.
    /// </para>
    /// <para>
    /// The same type is the body of a create — ICM's REST API guidance ("GET-PUT-POST
    /// Activities", ADO 8646): a PUT with <c>"Id": "NULL"</c>, profiled to a service
    /// request by <c>Activity SR Id</c>. Everything is a nullable string because that is
    /// what Siebel sends; typing it is <see cref="ActivityMapper"/>'s job.
    /// <see cref="AdditionalFields"/> catches anything not listed.
    /// </para>
    /// </remarks>
    internal class SiebelActivity
    {
        /// <summary>Gets or sets <c>Id</c>. The row id, the <c>activities_key</c>. Not in the document; always sent. <c>NULL</c> on a create.</summary>
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets <c>Activity SR Id</c>. The service request the activity is profiled to (max 15).</summary>
        [JsonPropertyName("Activity SR Id")]
        public string? ActivitySRId { get; set; }

        /// <summary>Gets or sets <c>SR Number</c> (max 64).</summary>
        [JsonPropertyName("SR Number")]
        public string? SRNumber { get; set; }

        /// <summary>Gets or sets <c>SR Type</c>; read-only.</summary>
        [JsonPropertyName("SR Type")]
        public string? SRType { get; set; }

        /// <summary>Gets or sets <c>SR Sub Type</c>; read-only.</summary>
        [JsonPropertyName("SR Sub Type")]
        public string? SRSubType { get; set; }

        /// <summary>Gets or sets <c>Case Id</c> (max 15).</summary>
        [JsonPropertyName("Case Id")]
        public string? CaseId { get; set; }

        /// <summary>Gets or sets <c>Case Number</c>; read-only.</summary>
        [JsonPropertyName("Case Number")]
        public string? CaseNumber { get; set; }

        /// <summary>Gets or sets <c>Incident Id</c> (max 15).</summary>
        [JsonPropertyName("Incident Id")]
        public string? IncidentId { get; set; }

        /// <summary>Gets or sets <c>ICM Memo Id</c> (max 15).</summary>
        [JsonPropertyName("ICM Memo Id")]
        public string? ICMMemoId { get; set; }

        /// <summary>Gets or sets <c>Activity Template Id</c>. On a plan step, the plan <i>instance</i> row id (MEASURED: equals the SR's <c>ActivityPlan</c> child's <c>Id</c>).</summary>
        [JsonPropertyName("Activity Template Id")]
        public string? ActivityTemplateId { get; set; }

        /// <summary>Gets or sets <c>Plan Name</c>, e.g. <c>Bus Pass - New Intake</c>; read-only.</summary>
        [JsonPropertyName("Plan Name")]
        public string? PlanName { get; set; }

        /// <summary>Gets or sets <c>Sequence</c>. The step's position in its plan, e.g. <c>01</c>, <c>01-01-01</c> (max 255).</summary>
        [JsonPropertyName("Sequence")]
        public string? Sequence { get; set; }

        /// <summary>Gets or sets <c>Type</c>, e.g. <c>Task</c>, <c>System Task</c> (max 30). Required.</summary>
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        /// <summary>Gets or sets <c>ICM Type</c>; read-only.</summary>
        [JsonPropertyName("ICM Type")]
        public string? ICMType { get; set; }

        /// <summary>Gets or sets <c>ICM Sub Type</c> (max 30).</summary>
        [JsonPropertyName("ICM Sub Type")]
        public string? ICMSubType { get; set; }

        /// <summary>Gets or sets <c>Category</c>, e.g. <c>General</c> (max 30). Required.</summary>
        [JsonPropertyName("Category")]
        public string? Category { get; set; }

        /// <summary>Gets or sets <c>Class</c> (max 30).</summary>
        [JsonPropertyName("Class")]
        public string? Class { get; set; }

        /// <summary>Gets or sets <c>Action By</c>: <c>Staff</c> or <c>System</c> (max 30). Required.</summary>
        [JsonPropertyName("Action By")]
        public string? ActionBy { get; set; }

        /// <summary>Gets or sets <c>Action Code</c>, the step's name, e.g. <c>Validate Address and Promote Prospect</c> (max 50).</summary>
        [JsonPropertyName("Action Code")]
        public string? ActionCode { get; set; }

        /// <summary>Gets or sets <c>Result Code</c>, the step's outcome, e.g. <c>Complete</c>, <c>Case Created</c> (max 50).</summary>
        [JsonPropertyName("Result Code")]
        public string? ResultCode { get; set; }

        /// <summary>Gets or sets <c>Description</c> (max 250).</summary>
        [JsonPropertyName("Description")]
        public string? Description { get; set; }

        /// <summary>Gets or sets <c>Display</c>, e.g. <c>Calendar and Activities</c> (max 30). Required.</summary>
        [JsonPropertyName("Display")]
        public string? Display { get; set; }

        /// <summary>Gets or sets <c>Status</c>: <c>Open</c>, <c>Closed</c>, <c>Cancelled</c> (max 30).</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>Done Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Done Flag")]
        public string? DoneFlag { get; set; }

        /// <summary>Gets or sets <c>Done</c> (<c>DTYPE_UTCDATETIME</c>). When it was completed.</summary>
        [JsonPropertyName("Done")]
        public string? Done { get; set; }

        /// <summary>Gets or sets <c>Due</c> (<c>DTYPE_UTCDATETIME</c>). Created + 30 days on a plan step (MEASURED).</summary>
        [JsonPropertyName("Due")]
        public string? Due { get; set; }

        /// <summary>Gets or sets <c>Planned</c> (<c>DTYPE_UTCDATETIME</c>).</summary>
        [JsonPropertyName("Planned")]
        public string? Planned { get; set; }

        /// <summary>Gets or sets <c>Planned Completion</c> (<c>DTYPE_UTCDATETIME</c>).</summary>
        [JsonPropertyName("Planned Completion")]
        public string? PlannedCompletion { get; set; }

        /// <summary>Gets or sets <c>Duration Minutes</c>.</summary>
        [JsonPropertyName("Duration Minutes")]
        public string? DurationMinutes { get; set; }

        /// <summary>Gets or sets <c>Priority</c>, e.g. <c>3-Standard</c> (max 30).</summary>
        [JsonPropertyName("Priority")]
        public string? Priority { get; set; }

        /// <summary>Gets or sets <c>Owned By</c>, the owner's login; read-only.</summary>
        [JsonPropertyName("Owned By")]
        public string? OwnedBy { get; set; }

        /// <summary>Gets or sets <c>Owned By Id</c>; read-only, yet required by the document (Siebel fills it).</summary>
        [JsonPropertyName("Owned By Id")]
        public string? OwnedById { get; set; }

        /// <summary>Gets or sets <c>Primary Owned By</c> (max 50). The login to assign the activity to.</summary>
        [JsonPropertyName("Primary Owned By")]
        public string? PrimaryOwnedBy { get; set; }

        /// <summary>Gets or sets <c>Primary Owner Id</c>. <c>No Match Row Id</c> on an unassigned step (MEASURED).</summary>
        [JsonPropertyName("Primary Owner Id")]
        public string? PrimaryOwnerId { get; set; }

        /// <summary>Gets or sets <c>Primary Contact Id</c> (max 15).</summary>
        [JsonPropertyName("Primary Contact Id")]
        public string? PrimaryContactId { get; set; }

        /// <summary>Gets or sets <c>Contact First Name</c> (max 50).</summary>
        [JsonPropertyName("Contact First Name")]
        public string? ContactFirstName { get; set; }

        /// <summary>Gets or sets <c>Contact Last Name</c> (max 50).</summary>
        [JsonPropertyName("Contact Last Name")]
        public string? ContactLastName { get; set; }

        /// <summary>Gets or sets <c>Service Office</c> (max 100).</summary>
        [JsonPropertyName("Service Office")]
        public string? ServiceOffice { get; set; }

        /// <summary>Gets or sets <c>ICM Service Region</c>; read-only.</summary>
        [JsonPropertyName("ICM Service Region")]
        public string? ICMServiceRegion { get; set; }

        /// <summary>Gets or sets <c>Ministry</c> (max 100).</summary>
        [JsonPropertyName("Ministry")]
        public string? Ministry { get; set; }

        /// <summary>Gets or sets <c>Ministry Id</c> (max 15).</summary>
        [JsonPropertyName("Ministry Id")]
        public string? MinistryId { get; set; }

        /// <summary>Gets or sets <c>ICM Created By Office</c> (max 100).</summary>
        [JsonPropertyName("ICM Created By Office")]
        public string? ICMCreatedByOffice { get; set; }

        /// <summary>Gets or sets <c>Restricted Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Restricted Flag")]
        public string? RestrictedFlag { get; set; }

        /// <summary>Gets or sets <c>Shared Flag</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Shared Flag")]
        public string? SharedFlag { get; set; }

        /// <summary>Gets or sets <c>Repeating</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Repeating")]
        public string? Repeating { get; set; }

        /// <summary>Gets or sets <c>Alarm</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Alarm")]
        public string? Alarm { get; set; }

        /// <summary>Gets or sets <c>Overdue</c> (<c>Y</c>/<c>N</c>); read-only.</summary>
        [JsonPropertyName("Overdue")]
        public string? Overdue { get; set; }

        /// <summary>Gets or sets <c>Show In Contact Profile</c> (<c>Y</c>/<c>N</c>).</summary>
        [JsonPropertyName("Show In Contact Profile")]
        public string? ShowInContactProfile { get; set; }

        /// <summary>Gets or sets <c>Created</c> (<c>DTYPE_UTCDATETIME</c> by the document; the SR's wall clock in practice); read-only.</summary>
        [JsonPropertyName("Created")]
        public string? Created { get; set; }

        /// <summary>Gets or sets <c>Created By</c>, the creating user's row id; read-only.</summary>
        [JsonPropertyName("Created By")]
        public string? CreatedBy { get; set; }

        /// <summary>Gets or sets <c>Created By Name</c>, the creating user's login.</summary>
        [JsonPropertyName("Created By Name")]
        public string? CreatedByName { get; set; }

        /// <summary>Gets or sets <c>Updated</c> (<c>DTYPE_UTCDATETIME</c>); read-only.</summary>
        [JsonPropertyName("Updated")]
        public string? Updated { get; set; }

        /// <summary>Gets or sets <c>Updated By</c>, the updating user's row id; read-only.</summary>
        [JsonPropertyName("Updated By")]
        public string? UpdatedBy { get; set; }

        /// <summary>Gets or sets <c>Updated By Name</c>; read-only.</summary>
        [JsonPropertyName("Updated By Name")]
        public string? UpdatedByName { get; set; }

        /// <summary>Gets or sets the record's links. Declared so they do not land in <see cref="AdditionalFields"/>. Not mapped.</summary>
        [JsonPropertyName("Link")]
        [JsonConverter(typeof(SiebelLinkListConverter))]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets or sets every field ICM sent that is not declared above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
