namespace Icm.Api.Models
{
    using System.Collections.Generic;

    /// <summary>One page of activities from a search.</summary>
    public class ActivityPage
    {
        /// <summary>Gets the activities on this page.</summary>
        public IReadOnlyList<Activity> Items { get; init; } = [];

        /// <summary>Gets the total number of matching rows, when asked for and supplied. Null otherwise.</summary>
        public long? TotalCount { get; init; }
    }
}
