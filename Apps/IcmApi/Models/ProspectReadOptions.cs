namespace Icm.Api.Models
{
    /// <summary>Options for reading one prospect by key, or the prospects on a service request.</summary>
    /// <remarks>
    /// The row, or the parent, is identified by its key, so nothing to do with searching
    /// or paging applies. What is left is visibility.
    /// </remarks>
    public class ProspectReadOptions
    {
        /// <summary>
        /// Gets or sets the Siebel visibility mode. Null or blank means <c>Organization</c>,
        /// for the reason <see cref="ProspectQuery.ViewMode"/> gives.
        /// </summary>
        public string? ViewMode { get; set; }
    }
}
