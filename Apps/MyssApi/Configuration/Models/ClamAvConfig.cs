namespace Myss.Api.Configuration.Models
{
    /// <summary>
    /// Connection settings for the ClamAV daemon, bound from the <c>ClamAv</c>
    /// section. clamd speaks its own TCP protocol, not HTTP.
    /// </summary>
    public class ClamAvConfig
    {
        /// <summary>
        /// Gets or sets the clamd host: the compose service locally, the
        /// in-cluster Service (<c>clamav.&lt;namespace&gt;.svc</c>) deployed.
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the clamd TCP port.
        /// </summary>
        public int Port { get; set; } = 3310;

        /// <summary>
        /// Gets or sets the connect/scan timeout in seconds. Generous because
        /// a file near the size cap can take a while to scan.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 120;
    }
}
