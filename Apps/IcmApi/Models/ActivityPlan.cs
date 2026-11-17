namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>An ICM activity plan instance: a plan template applied to a service request, whose steps are <see cref="Activity"/> rows.</summary>
    /// <remarks>
    /// The bus pass workflow applies <c>Bus Pass - New Intake</c> (template
    /// <c>1-30LDVGD</c>) to an Application SR and <c>Bus Pass – Address Change</c> to a
    /// Change of Circumstance; each step's <see cref="Activity.ActivityPlanId"/> is this
    /// row's <see cref="Id"/>. Read-only, and not creatable through the REST API as it
    /// stands (MEASURED 2026-09-29: the write-side <c>Template</c> picklist refuses the
    /// bus pass template).
    /// </remarks>
    public class ActivityPlan
    {
        /// <summary>Gets <c>Id</c>. The row id, which each step's <see cref="Activity.ActivityPlanId"/> names.</summary>
        public string? Id { get; init; }

        /// <summary>Gets <c>Activity SR Id</c>. The service request the plan is applied to.</summary>
        public string? ServiceRequestId { get; init; }

        /// <summary>Gets <c>SR Number</c>.</summary>
        public string? ServiceRequestNumber { get; init; }

        /// <summary>Gets <c>SR Type</c>.</summary>
        public string? ServiceRequestType { get; init; }

        /// <summary>Gets <c>SR Sub Type</c>.</summary>
        public string? ServiceRequestSubType { get; init; }

        /// <summary>Gets <c>Case Id</c>.</summary>
        public string? CaseId { get; init; }

        /// <summary>Gets <c>Incident Id</c>.</summary>
        public string? IncidentId { get; init; }

        /// <summary>Gets <c>Template</c>, the plan template's name.</summary>
        public string? Template { get; init; }

        /// <summary>Gets <c>Template Id</c>, the plan template's row id.</summary>
        public string? TemplateId { get; init; }

        /// <summary>Gets <c>Status</c>: <c>In Progress</c>, <c>Closed</c>, <c>Cancelled</c>.</summary>
        public string? Status { get; init; }

        /// <summary>Gets <c>Comments</c>.</summary>
        public string? Comments { get; init; }

        /// <summary>Gets <c>Completed Date</c>, a zone-less date and time as written.</summary>
        public DateTime? CompletedDate { get; init; }

        /// <summary>Gets <c>Restricted Flag</c>.</summary>
        public bool? IsRestricted { get; init; }

        /// <summary>Gets <c>ICM Created By Office</c>.</summary>
        public string? CreatedByOffice { get; init; }

        /// <summary>Gets <c>Created By Login</c>.</summary>
        public string? CreatedBy { get; init; }

        /// <summary>Gets anything ICM sent that is not modelled above, as raw JSON.</summary>
        public IReadOnlyDictionary<string, JsonElement> AdditionalFields { get; init; } =
            new Dictionary<string, JsonElement>();

        /// <summary>Gets the values that arrived in a shape the client could not read, keyed by ICM field name.</summary>
        public IReadOnlyDictionary<string, string> UnparsedValues { get; init; } =
            new Dictionary<string, string>();
    }
}
