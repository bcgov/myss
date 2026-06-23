namespace Myss.Api.Configuration.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Settings to control request logging.
    /// </summary>
    public class RequestLoggingConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether Open Telemetry is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the optional request paths to exclude. Handles a * wildcard as a prefix or postfix.
        /// </summary>
        public IEnumerable<string>? ExcludedPaths { get; set; }
    }
}
