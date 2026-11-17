namespace Icm.Api.Models
{
    /// <summary>An activity plan search on the standalone path <c>ActivityPlan/ActivityPlan</c>.</summary>
    /// <remarks>Criteria are ANDed, at least one must be set, and values are vetted as a contact search's are.</remarks>
    public class ActivityPlanQuery
    {
        /// <summary>Gets or sets the ICM row id to match (<c>Id</c>).</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the service request row id to match (<c>Activity SR Id</c>).</summary>
        public string? ServiceRequestId { get; set; }

        /// <summary>Gets or sets the template name to match (<c>Template</c>), e.g. <c>Bus Pass - New Intake</c>.</summary>
        public string? Template { get; set; }

        /// <summary>Gets or sets the template row id to match (<c>Template Id</c>), e.g. <c>1-30LDVGD</c>.</summary>
        public string? TemplateId { get; set; }

        /// <summary>Gets or sets the status to match (<c>Status</c>).</summary>
        public string? Status { get; set; }

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
