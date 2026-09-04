namespace Icm.Api.Contracts
{
    using Refit;

    /// <summary>
    /// The query string of the single-record GET on
    /// <c>data/ServiceRequest/ServiceRequest/{servicerequest_key}/</c>.
    /// </summary>
    /// <remarks>
    /// A strict subset of <see cref="SiebelListQuery"/> — the record is identified by
    /// the key in the path, so nothing to do with searching, sorting or paging applies. Kept
    /// as its own type so the signature cannot offer parameters Siebel would ignore.
    /// </remarks>
    internal class SiebelItemQuery
    {
        /// <summary>
        /// Gets or sets <c>fields</c>, a comma-separated list of field names to return.
        /// </summary>
        [AliasAs("fields")]
        public string? Fields { get; set; }

        /// <summary>
        /// Gets or sets <c>childlinks</c>, the child business components to link in the
        /// response.
        /// </summary>
        [AliasAs("childlinks")]
        public string? ChildLinks { get; set; }

        /// <summary>
        /// Gets or sets <c>ViewMode</c>, the Siebel visibility mode. Siebel defaults to
        /// <c>Sales Rep</c>, which will 404 a record the authenticated user does not own.
        /// </summary>
        [AliasAs("ViewMode")]
        public string? ViewMode { get; set; }

        /// <summary>
        /// Gets or sets <c>excludeEmptyFieldsInResponse</c>. When true Siebel omits empty
        /// fields instead of returning them as empty strings.
        /// </summary>
        [AliasAs("excludeEmptyFieldsInResponse")]
        public bool? ExcludeEmptyFieldsInResponse { get; set; }
    }
}
