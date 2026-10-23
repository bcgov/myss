namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The body of a successful activity list read — a search of
    /// <c>data/Activities/Activities/</c> or a service request's <c>Activities</c> child.
    /// One type for both, as for prospects: the child read adds <c>lastpage</c>.
    /// </summary>
    internal class SiebelActivityListResponse
    {
        /// <summary>Gets or sets <c>lastpage</c>, as the string <c>"true"</c>; <see cref="IsLastPage"/> reads it.</summary>
        [JsonPropertyName("lastpage")]
        public string? LastPage { get; set; }

        /// <summary>Gets or sets the matching rows.</summary>
        [JsonPropertyName("items")]
        public IList<SiebelActivity>? Items { get; set; }

        /// <summary>Gets or sets the paging links for the result set.</summary>
        [JsonPropertyName("Link")]
        [JsonConverter(typeof(SiebelLinkListConverter))]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets a value indicating whether Siebel said this page is the last one.</summary>
        [JsonIgnore]
        public bool IsLastPage => bool.TryParse(LastPage, out bool last) && last;
    }

    /// <summary>The body of a successful activity PUT: <c>items</c> a single object.</summary>
    internal class SiebelActivityWriteResponse
    {
        /// <summary>Gets or sets the written row.</summary>
        [JsonPropertyName("items")]
        public SiebelActivity? Items { get; set; }
    }

    /// <summary>
    /// The body of a successful activity plan list read — a search of
    /// <c>data/ActivityPlan/ActivityPlan/</c> or a service request's <c>ActivityPlan</c> child.
    /// </summary>
    internal class SiebelActivityPlanListResponse
    {
        /// <summary>Gets or sets <c>lastpage</c>, as the string <c>"true"</c>; <see cref="IsLastPage"/> reads it.</summary>
        [JsonPropertyName("lastpage")]
        public string? LastPage { get; set; }

        /// <summary>Gets or sets the matching rows.</summary>
        [JsonPropertyName("items")]
        public IList<SiebelActivityPlan>? Items { get; set; }

        /// <summary>Gets or sets the paging links for the result set.</summary>
        [JsonPropertyName("Link")]
        [JsonConverter(typeof(SiebelLinkListConverter))]
        public IList<SiebelLink>? Link { get; set; }

        /// <summary>Gets a value indicating whether Siebel said this page is the last one.</summary>
        [JsonIgnore]
        public bool IsLastPage => bool.TryParse(LastPage, out bool last) && last;
    }
}
