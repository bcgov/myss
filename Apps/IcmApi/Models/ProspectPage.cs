namespace Icm.Api.Models
{
    using System.Collections.Generic;

    /// <summary>One page of prospects from a search.</summary>
    /// <remarks>
    /// A search that matched nothing is an empty <see cref="Items"/>, not a null and not an
    /// exception — and, as ICM reports "no such row" and "not yours to see" the same way,
    /// it may also be the visibility mode.
    /// </remarks>
    public class ProspectPage
    {
        /// <summary>Gets the prospects on this page.</summary>
        public IReadOnlyList<Prospect> Items { get; init; } = [];

        /// <summary>
        /// Gets the total number of matching rows, when the search asked for it
        /// (<see cref="ProspectQuery.IncludeTotalCount"/>) and ICM supplied its
        /// <c>Total-Record-Count</c> header. Null otherwise.
        /// </summary>
        public long? TotalCount { get; init; }
    }
}
