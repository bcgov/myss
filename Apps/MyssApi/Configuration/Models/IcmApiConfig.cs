namespace Myss.Api.Configuration.Models
{
    using System;

    /// <summary>
    /// Settings for the ICM middleware (IcmApi), bound from the <c>IcmApi</c>
    /// section. MyssApi never talks to Siebel directly: the middleware owns the
    /// ICM credentials and the Siebel translation, and this API only speaks its
    /// business-language REST contract. Nothing ICM-shaped belongs in here.
    /// </summary>
    public class IcmApiConfig
    {
        /// <summary>
        /// Gets or sets the middleware base URL: the in-cluster Service when
        /// deployed, a local instance in development. Required; the app refuses
        /// to start without it.
        /// </summary>
        public Uri? BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the per-attempt timeout in seconds. The middleware waits
        /// on ICM behind this call, so the default is generous.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the service credentials MyssApi presents to the middleware.
        /// </summary>
        public IcmApiAuthConfig Auth { get; set; } = new();

        /// <summary>
        /// Gets a value indicating whether the app can start. Only the non-secret
        /// base URL is checked at startup; the credentials are validated at the
        /// first call instead, so a checkout without secrets still boots for tests.
        /// </summary>
        public bool IsConfigured => BaseUrl is not null && BaseUrl.IsAbsoluteUri;
    }
}
