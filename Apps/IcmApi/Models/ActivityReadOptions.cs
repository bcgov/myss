namespace Icm.Api.Models
{
    /// <summary>Options for reading one activity by key, or the activities or plans on a service request.</summary>
    public class ActivityReadOptions
    {
        /// <summary>Gets or sets the Siebel visibility mode. Null or blank means <c>Organization</c>.</summary>
        public string? ViewMode { get; set; }
    }
}
