namespace Icm.Api.Models
{
    /// <summary>An activity search on the standalone path: what to match, and how much to return.</summary>
    /// <remarks>
    /// Every criterion set must match (they are joined with <c>AND</c>), at least one must
    /// be set, and values are vetted exactly as a contact search's are — no raw expression,
    /// no wildcards, no blanks (see <see cref="ContactQuery"/>). To list the activities on
    /// a service request, <see cref="Services.IActivityService.GetForServiceRequestAsync"/>
    /// reads the child collection; <see cref="ServiceRequestId"/> here searches the same
    /// link from the standalone side (both MEASURED 2026-09-29).
    /// </remarks>
    public class ActivityQuery
    {
        /// <summary>Gets or sets the ICM row id to match (<c>Id</c>).</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the service request row id to match (<c>Activity SR Id</c>).</summary>
        public string? ServiceRequestId { get; set; }

        /// <summary>Gets or sets the case row id to match (<c>Case Id</c>).</summary>
        public string? CaseId { get; set; }

        /// <summary>Gets or sets the plan name to match (<c>Plan Name</c>), e.g. <c>Bus Pass - New Intake</c>.</summary>
        public string? PlanName { get; set; }

        /// <summary>Gets or sets the status to match (<c>Status</c>).</summary>
        public string? Status { get; set; }

        /// <summary>Gets or sets the type to match (<c>Type</c>).</summary>
        public string? Type { get; set; }

        /// <summary>Gets or sets the records per page. ICM accepts 1 to 100.</summary>
        public int? PageSize { get; set; }

        /// <summary>Gets or sets the zero-based index of the first record to return.</summary>
        public int? StartRowNum { get; set; }

        /// <summary>Gets or sets the Siebel visibility mode. Null or blank means <c>Organization</c>.</summary>
        public string? ViewMode { get; set; }

        /// <summary>Gets or sets a value indicating whether ICM should count every match as well.</summary>
        public bool? IncludeTotalCount { get; set; }
    }
}
