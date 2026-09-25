namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The body of a successful contact search
    /// (<c>data_ICMContact_ICMContact_get_all_response</c>).
    /// </summary>
    /// <remarks>
    /// As with <see cref="SiebelListResponse"/>, an empty result is a status code and no
    /// usable body rather than an empty <see cref="Items"/> array, so the repository
    /// decides what "nothing found" looks like to a caller.
    /// </remarks>
    internal class SiebelContactListResponse
    {
        /// <summary>Gets or sets the matching records.</summary>
        [JsonPropertyName("items")]
        public IList<SiebelContact>? Items { get; set; }

        /// <summary>Gets or sets the paging links for the result set.</summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }
    }
}
