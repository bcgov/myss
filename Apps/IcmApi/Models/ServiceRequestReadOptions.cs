namespace Icm.Api.Models
{
    using System.Collections.Generic;

    /// <summary>Options for reading a single service request by key.</summary>
    /// <remarks>
    /// A strict subset of <see cref="ServiceRequestQuery"/> — the record is identified by
    /// its key, so nothing to do with searching, sorting or paging applies. Its own type so
    /// the signature cannot offer parameters ICM would ignore.
    /// </remarks>
    public class ServiceRequestReadOptions
    {
        /// <summary>
        /// Gets or sets the fields to return, by their ICM names. Null returns them all.
        /// </summary>
        public IEnumerable<string>? Fields { get; set; }

        /// <summary>Gets or sets the child business components to link in the response.</summary>
        public string? ChildLinks { get; set; }

        /// <summary>
        /// Gets or sets the Siebel visibility mode. ICM defaults to <c>Sales Rep</c>, which
        /// reports a record the authenticated user does not own as missing.
        /// </summary>
        public string? ViewMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ICM should omit empty fields rather than
        /// return them as empty strings.
        /// </summary>
        public bool? ExcludeEmptyFields { get; set; }
    }
}
