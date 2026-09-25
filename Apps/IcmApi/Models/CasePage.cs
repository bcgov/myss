namespace Icm.Api.Models
{
    using System.Collections.Generic;

    /// <summary>One page of cases from a search.</summary>
    /// <remarks>
    /// A search that matched nothing is an empty <see cref="Items"/>, not a null and not an
    /// exception — and, as ICM reports "no such case" and "not yours to see" the same way,
    /// it may also be the visibility mode.
    /// </remarks>
    public class CasePage
    {
        /// <summary>Gets the cases on this page.</summary>
        public IReadOnlyList<Case> Items { get; init; } = [];

        /// <summary>
        /// Gets the total number of matching cases, when the search asked for it
        /// (<see cref="CaseQuery.IncludeTotalCount"/>) and ICM supplied its
        /// <c>Total-Record-Count</c> header. Null otherwise.
        /// </summary>
        public long? TotalCount { get; init; }
    }
}
