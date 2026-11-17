namespace Myss.Api.Configuration.Models
{
    using System;

    /// <summary>
    /// The BC Gov Notify service this API sends email through, bound from
    /// <c>Notify</c> (MYSS-36). Checked at the first send, not at startup, so a
    /// checkout without the API key still boots for tests; a send without it
    /// fails and says why.
    /// </summary>
    public class NotifyConfig
    {
        /// <summary>The configuration section the settings live in.</summary>
        public const string SectionName = "Notify";

        /// <summary>
        /// Gets or sets the Notify base URL, without the <c>/api/v1</c> path,
        /// e.g. <c>https://test.notify.digital.gov.bc.ca</c>.
        /// </summary>
        public Uri? BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the API key bound to the MySS Notify tenant, sent as
        /// <c>X-API-KEY</c>. Never commit a value.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>Gets or sets how long one send may take, in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Gets a value indicating whether a send can be attempted: an HTTPS base
        /// URL, so the API key never travels in cleartext, and a key.
        /// </summary>
        public bool IsConfigured =>
            BaseUrl is { IsAbsoluteUri: true } url
                && url.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrWhiteSpace(ApiKey);
    }
}
