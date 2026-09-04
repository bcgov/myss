namespace Icm.Api.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The body of a successful list GET
    /// (<c>data_ServiceRequest_ServiceRequest_get_all_response</c>).
    /// </summary>
    /// <remarks>
    /// Siebel answers an empty result with <c>204</c> and no body rather than an empty
    /// <see cref="Items"/> array, so the repository — not this type — decides what "nothing
    /// found" looks like to a caller.
    /// </remarks>
    internal class SiebelListResponse
    {
        /// <summary>Gets or sets the matching records.</summary>
        [JsonPropertyName("items")]
        public IList<SiebelServiceRequest>? Items { get; set; }

        /// <summary>Gets or sets the paging links for the result set.</summary>
        [JsonPropertyName("Link")]
        public IList<SiebelLink>? Link { get; set; }
    }

    /// <summary>
    /// The body of a successful POST or PUT
    /// (<c>data_ServiceRequest_ServiceRequest_put_post_response</c>).
    /// </summary>
    /// <remarks>
    /// <c>items</c> is a single object here, not the array the list response uses. That
    /// asymmetry is Siebel's, and is mirrored rather than smoothed over — smoothing it is
    /// the repository's job.
    /// </remarks>
    internal class SiebelWriteResponse
    {
        /// <summary>Gets or sets the written record.</summary>
        [JsonPropertyName("items")]
        public SiebelServiceRequest? Items { get; set; }
    }

    /// <summary>
    /// The body of a successful DELETE
    /// (<c>data_ServiceRequest_ServiceRequest_del_response</c>): an untyped empty object.
    /// </summary>
    internal class SiebelDeleteResponse
    {
    }
}
