namespace Icm.Api.Models
{
    using System.Collections.Generic;

    /// <summary>Search, paging and field-selection parameters for a service request search.</summary>
    /// <remarks>
    /// <para>
    /// Deliberately smaller than the query string the spec defines. <c>uniformresponse</c>,
    /// <c>pagination</c> and <c>ExecutionMode</c> are protocol and engine details that a
    /// caller has no business setting, so they are fixed or left to ICM's own defaults.
    /// </para>
    /// <para>
    /// <see cref="SearchSpec"/> and <see cref="SortSpec"/> are the exception: they are raw
    /// Siebel expressions, and a leak of the upstream language into this model. Wrapping
    /// them would mean building a query language, which is not worth it for the handful of
    /// searches MySS makes — but if that changes, this is the property to replace.
    /// </para>
    /// </remarks>
    public class ServiceRequestQuery
    {
        /// <summary>
        /// Gets or sets the Siebel search expression, e.g. <c>[Status] = "Open"</c>.
        /// Field names here are the OpenAPI document's, not the response's — and not
        /// every response field is searchable: ICM rejects <c>[SR Number]</c> outright,
        /// so the record number cannot be searched on at all.
        /// </summary>
        public string? SearchSpec { get; set; }

        /// <summary>Gets or sets the Siebel sort expression.</summary>
        public string? SortSpec { get; set; }

        /// <summary>
        /// Gets or sets the fields to return, by their ICM names. Null returns them all.
        /// Narrowing this is the cheapest way to keep a response small.
        /// </summary>
        public IEnumerable<string>? Fields { get; set; }

        /// <summary>Gets or sets the child business components to link in the response.</summary>
        public string? ChildLinks { get; set; }

        /// <summary>Gets or sets the records per page. ICM accepts 1 to 100.</summary>
        public int? PageSize { get; set; }

        /// <summary>Gets or sets the zero-based index of the first record to return.</summary>
        public int? StartRowNum { get; set; }

        /// <summary>
        /// Gets or sets the Siebel visibility mode. ICM defaults to <c>Sales Rep</c>, which
        /// restricts the result to records the authenticated user owns — widen it
        /// deliberately, not by habit.
        /// </summary>
        public string? ViewMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ICM should count the whole result set as
        /// well as returning the page. It costs an extra pass over the data; the answer
        /// arrives in <see cref="ServiceRequestPage.TotalCount"/>.
        /// </summary>
        public bool? IncludeTotalCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ICM should omit empty fields rather than
        /// return them as empty strings.
        /// </summary>
        public bool? ExcludeEmptyFields { get; set; }
    }
}
