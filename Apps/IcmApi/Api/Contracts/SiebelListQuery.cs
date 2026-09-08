namespace Icm.Api.Contracts
{
    using Refit;

    /// <summary>
    /// The query string of the list GET on <c>data/ServiceRequest/ServiceRequest/</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every property is nullable and unset properties are left off the URL entirely, so
    /// Siebel applies its own documented default rather than one restated here — the one
    /// exception is <see cref="UniformResponse"/>, which the spec marks required.
    /// </para>
    /// <para>
    /// The <see cref="AliasAsAttribute"/> on each property carries the parameter name
    /// Siebel expects; several are lower-case and one is not, and that is deliberate.
    /// </para>
    /// </remarks>
    internal class SiebelListQuery
    {
        /// <summary>
        /// Gets or sets <c>uniformresponse</c>. Required; the spec permits only
        /// <see cref="SiebelFlag.Yes"/>, which makes Siebel return a single-record result in
        /// the same <c>items</c> array shape as a multi-record one. Defaulted here because a
        /// request without it is rejected.
        /// </summary>
        [AliasAs("uniformresponse")]
        public string? UniformResponse { get; set; } = SiebelFlag.Yes;

        /// <summary>
        /// Gets or sets <c>searchspec</c>, the Siebel search expression, e.g.
        /// <c>[SR Number] = "1-12345"</c>.
        /// </summary>
        [AliasAs("searchspec")]
        public string? SearchSpec { get; set; }

        /// <summary>Gets or sets <c>sortspec</c>, the Siebel sort expression.</summary>
        [AliasAs("sortspec")]
        public string? SortSpec { get; set; }

        /// <summary>
        /// Gets or sets <c>fields</c>, a comma-separated list of field names to return.
        /// Narrowing this is the cheapest way to keep a response small.
        /// </summary>
        [AliasAs("fields")]
        public string? Fields { get; set; }

        /// <summary>
        /// Gets or sets <c>childlinks</c>, the child business components to link in the
        /// response.
        /// </summary>
        [AliasAs("childlinks")]
        public string? ChildLinks { get; set; }

        /// <summary>Gets or sets <c>PageSize</c>, the records per page (1 to 100).</summary>
        [AliasAs("PageSize")]
        public int? PageSize { get; set; }

        /// <summary>
        /// Gets or sets <c>StartRowNum</c>, the zero-based index of the first record to
        /// return.
        /// </summary>
        [AliasAs("StartRowNum")]
        public int? StartRowNum { get; set; }

        /// <summary>
        /// Gets or sets <c>pagination</c>: <see cref="SiebelFlag.Yes"/> or
        /// <see cref="SiebelFlag.No"/>. Siebel defaults to <c>Y</c>.
        /// </summary>
        [AliasAs("pagination")]
        public string? Pagination { get; set; }

        /// <summary>
        /// Gets or sets <c>ViewMode</c>, the Siebel visibility mode applied to the query.
        /// Siebel defaults to <c>Sales Rep</c>, which restricts the result to records the
        /// authenticated user owns — widen it deliberately, not by habit.
        /// </summary>
        [AliasAs("ViewMode")]
        public string? ViewMode { get; set; }

        /// <summary>
        /// Gets or sets <c>recordcountneeded</c>. When true Siebel counts the full result
        /// set as well as returning the page, which costs an extra pass over the data.
        /// </summary>
        [AliasAs("recordcountneeded")]
        public bool? RecordCountNeeded { get; set; }

        /// <summary>
        /// Gets or sets <c>ExecutionMode</c>. Siebel defaults to <c>BiDirectional</c>.
        /// </summary>
        [AliasAs("ExecutionMode")]
        public string? ExecutionMode { get; set; }

        /// <summary>
        /// Gets or sets <c>excludeEmptyFieldsInResponse</c>. When true Siebel omits empty
        /// fields instead of returning them as empty strings.
        /// </summary>
        /// <remarks>
        /// The spec types this one as a string with a default of <c>"false"</c> rather than
        /// as a boolean; it is exposed as a <see cref="bool"/> and rendered lower-case by
        /// <see cref="Icm.Api.SiebelUrlParameterFormatter"/>.
        /// </remarks>
        [AliasAs("excludeEmptyFieldsInResponse")]
        public bool? ExcludeEmptyFieldsInResponse { get; set; }
    }
}
