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
    }
}
