namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The body of a successful case search (<c>data_Cases_Case_get_all_response</c>).
    /// </summary>
    /// <remarks>
    /// As with <see cref="SiebelListResponse"/>, an empty result is a status code and no
    /// usable body rather than an empty <see cref="Items"/> array, so the repository
    /// decides what "nothing found" looks like to a caller.
    /// </remarks>
    internal class SiebelCaseListResponse
    {
        /// <summary>Gets or sets the matching records.</summary>
        [JsonPropertyName("items")]
        public IList<SiebelCase>? Items { get; set; }

        /// <summary>Gets or sets the paging links for the result set.</summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }
    }

    /// <summary>
    /// The body of a successful read of a case's <c>Contact</c> child collection. Not in
    /// any describe document; the shape is the list shape, MEASURED on 2026-09-24, plus a
    /// <c>lastpage</c> flag the parent list does not send.
    /// </summary>
    internal class SiebelCaseContactListResponse
    {
        /// <summary>
        /// Gets or sets <c>lastpage</c>: Siebel's word on whether this page is the last one.
        /// Arrives as the string <c>"true"</c> (MEASURED 2026-09-24 on a two-row case), so
        /// it is kept as text; <see cref="IsLastPage"/> reads it.
        /// </summary>
        [JsonPropertyName("lastpage")]
        public string? LastPage { get; set; }

        /// <summary>Gets or sets the people on the case.</summary>
        [JsonPropertyName("items")]
        public IList<SiebelCaseContact>? Items { get; set; }

        /// <summary>Gets or sets the paging links for the result set.</summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>
        /// Gets a value indicating whether Siebel said this page is the last one. Only an
        /// explicit <c>"true"</c> counts: a missing flag means there may be more, and the
        /// repository keeps reading until Siebel says otherwise or runs out of rows.
        /// </summary>
        [JsonIgnore]
        public bool IsLastPage => bool.TryParse(LastPage, out bool last) && last;
    }
}
