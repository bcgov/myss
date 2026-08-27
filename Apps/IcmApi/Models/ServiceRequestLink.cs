namespace Icm.Api.Models
{
    /// <summary>A link ICM returned alongside a record or a page of records.</summary>
    public class ServiceRequestLink
    {
        /// <summary>Gets the relation, e.g. <c>self</c> or a child business component.</summary>
        public string? Rel { get; init; }

        /// <summary>
        /// Gets the target address. A string rather than a <see cref="System.Uri"/> because
        /// ICM is free to return a relative or malformed value, and losing the link
        /// entirely would be worse than handing over what arrived.
        /// </summary>
        public string? Href { get; init; }

        /// <summary>Gets the link name.</summary>
        public string? Name { get; init; }
    }
}
