namespace Icm.Api.Models
{
    /// <summary>A prospect search on the standalone path: what to match, and how much to return.</summary>
    /// <remarks>
    /// <para>
    /// <b>Every criterion that is set must match</b> — they are joined with <c>AND</c> —
    /// and at least one must be set. Null means "not a criterion"; a blank string is
    /// refused, for the reasons <see cref="ContactQuery"/> gives.
    /// </para>
    /// <para>
    /// <b>There is no raw search expression here, deliberately</b>, and the values are
    /// vetted exactly as a contact search's are: a double quote, a wildcard or a control
    /// character is refused with an <see cref="System.ArgumentException"/> naming the
    /// property, never the value. Every criterion is matched exactly and case-sensitively.
    /// </para>
    /// <para>
    /// To list the prospects <i>on</i> a service request, prefer
    /// <see cref="Services.IProspectService.GetForServiceRequestAsync"/>, which reads the
    /// child collection the workflow populates; <see cref="ServiceRequestId"/> here searches
    /// the same link from the standalone side, which is not yet MEASURED to work.
    /// </para>
    /// </remarks>
    public class ProspectQuery
    {
        /// <summary>Gets or sets the ICM row id to match (<c>Id</c>).</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the service request row id to match (<c>Service Request Id</c>).</summary>
        public string? ServiceRequestId { get; set; }

        /// <summary>Gets or sets the prospective contact UId to match (<c>Prospective Contact UId</c>).</summary>
        public string? ProspectiveContactUId { get; set; }

        /// <summary>Gets or sets the source to match (<c>Source</c>).</summary>
        public string? Source { get; set; }

        /// <summary>Gets or sets the records per page. ICM accepts 1 to 100.</summary>
        public int? PageSize { get; set; }

        /// <summary>Gets or sets the zero-based index of the first record to return.</summary>
        public int? StartRowNum { get; set; }

        /// <summary>
        /// Gets or sets the Siebel visibility mode. Null or blank means <c>Organization</c>,
        /// the mode every prospect read so far was made under (as a child of a service
        /// request, which needs it), not ICM's default.
        /// </summary>
        public string? ViewMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ICM should count every match as well as
        /// returning the page; the answer arrives in <see cref="ProspectPage.TotalCount"/>.
        /// </summary>
        public bool? IncludeTotalCount { get; set; }
    }
}
