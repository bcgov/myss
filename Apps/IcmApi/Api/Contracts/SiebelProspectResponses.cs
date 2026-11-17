namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The body of a successful prospect list read — a search of
    /// <c>data/Prospects/SRProspects/</c> (<c>data_Prospects_SRProspects_get_all_response</c>)
    /// or a service request's <c>SRProspects</c> child collection.
    /// </summary>
    /// <remarks>
    /// One type for both because the shape is the same list shape, with the child read
    /// adding the <c>lastpage</c> flag every child collection sends (MEASURED 2026-09-29,
    /// <c>"lastpage": "true"</c> on the one-row reads). As with the other list responses,
    /// an empty result is a status code and no usable body rather than an empty
    /// <see cref="Items"/>, so the repository decides what "nothing" looks like to a caller.
    /// </remarks>
    internal class SiebelProspectListResponse
    {
        /// <summary>
        /// Gets or sets <c>lastpage</c>: Siebel's word on whether this page is the last one,
        /// as the string <c>"true"</c>; <see cref="IsLastPage"/> reads it. Sent by the child
        /// read; not expected from the standalone search.
        /// </summary>
        [JsonPropertyName("lastpage")]
        public string? LastPage { get; set; }

        /// <summary>Gets or sets the matching rows.</summary>
        [JsonPropertyName("items")]
        public IList<SiebelProspect>? Items { get; set; }

        /// <summary>Gets or sets the paging links for the result set.</summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>
        /// Gets a value indicating whether Siebel said this page is the last one. Only an
        /// explicit <c>"true"</c> counts, as for the case's contact child.
        /// </summary>
        [JsonIgnore]
        public bool IsLastPage => bool.TryParse(LastPage, out bool last) && last;
    }

    /// <summary>
    /// The body of a successful prospect POST or PUT
    /// (<c>data_Prospects_SRProspects_put_post_response</c>): <c>items</c> a single object,
    /// as on the service request write, not the array the list uses.
    /// </summary>
    internal class SiebelProspectWriteResponse
    {
        /// <summary>Gets or sets the written row.</summary>
        [JsonPropertyName("items")]
        public SiebelProspect? Items { get; set; }
    }
}
