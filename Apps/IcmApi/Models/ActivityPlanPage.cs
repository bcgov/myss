namespace Icm.Api.Models
{
    using System.Collections.Generic;

    /// <summary>One page of activity plans from a search.</summary>
    public class ActivityPlanPage
    {
        /// <summary>Gets the plans on this page.</summary>
        public IReadOnlyList<ActivityPlan> Items { get; init; } = [];

        /// <summary>Gets the total number of matching rows, when asked for and supplied. Null otherwise.</summary>
        public long? TotalCount { get; init; }
    }
}
