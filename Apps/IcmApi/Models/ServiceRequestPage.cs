namespace Icm.Api.Models
{
    using System.Collections.Generic;

    /// <summary>One page of service requests from a search.</summary>
    /// <remarks>
    /// A search that matched nothing is an empty <see cref="Items"/>, not a null and not an
    /// exception — ICM signals it with a <c>204</c>, and translating that is the
    /// repository's job rather than every caller's.
    /// </remarks>
    public class ServiceRequestPage
    {
        /// <summary>Gets the records on this page.</summary>
        public IReadOnlyList<ServiceRequest> Items { get; init; } = [];

        /// <summary>Gets the paging links ICM returned, if any.</summary>
        public IReadOnlyList<ServiceRequestLink> Links { get; init; } = [];

        /// <summary>
        /// Gets the total number of matching records, when the search asked for it
        /// (<see cref="ServiceRequestQuery.IncludeTotalCount"/>) and ICM supplied its
        /// <c>Total-Record-Count</c> header. Null otherwise.
        /// </summary>
        public long? TotalCount { get; init; }
    }
}
