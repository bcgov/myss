namespace Icm.Api.Models
{
    /// <summary>Options for reading one case, or the people on it, by the case's key.</summary>
    /// <remarks>
    /// The record is identified by its key, so nothing to do with searching or paging
    /// applies, and the field list is fixed by the library. What is left is visibility.
    /// </remarks>
    public class CaseReadOptions
    {
        /// <summary>
        /// Gets or sets the Siebel visibility mode. Null or blank means <c>Manager</c>, for
        /// the reason <see cref="CaseQuery.ViewMode"/> gives.
        /// </summary>
        public string? ViewMode { get; set; }
    }
}
