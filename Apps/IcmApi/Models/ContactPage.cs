namespace Icm.Api.Models
{
    using System.Collections.Generic;

    /// <summary>One page of contacts from a search.</summary>
    /// <remarks>
    /// A search that matched nothing is an empty <see cref="Items"/>, not a null and not an
    /// exception. When the search is meant to identify one person, anything other than
    /// exactly one item means it has not: more than one is "cannot tell who this is",
    /// never "take the first".
    /// </remarks>
    public class ContactPage
    {
        /// <summary>Gets the contacts on this page.</summary>
        public IReadOnlyList<Contact> Items { get; init; } = [];

        /// <summary>
        /// Gets the total number of matching contacts, when the search asked for it
        /// (<see cref="ContactQuery.IncludeTotalCount"/>) and ICM supplied its
        /// <c>Total-Record-Count</c> header. Null otherwise.
        /// </summary>
        public long? TotalCount { get; init; }
    }
}
