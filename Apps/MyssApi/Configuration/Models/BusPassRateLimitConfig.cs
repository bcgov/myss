namespace Myss.Api.Configuration.Models
{
    /// <summary>
    /// The per-address limit on anonymous bus pass submissions, bound from
    /// <c>BusPass:SubmitRateLimit</c>. Every accepted submission files a service
    /// request in ICM, so the public route cannot be left unthrottled.
    /// </summary>
    public class BusPassRateLimitConfig
    {
        /// <summary>Gets or sets how many submissions one address may make per window.</summary>
        public int PermitLimit { get; set; } = 5;

        /// <summary>Gets or sets the window length in seconds.</summary>
        public int WindowSeconds { get; set; } = 60;
    }
}
