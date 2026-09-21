namespace Icm.Api.Host.Configuration.Models
{
    using System;

    /// <summary>
    /// Where ICM is and how to authenticate to it, bound from the <c>Icm</c>
    /// section. The same shape IcmApi.Console reads, so one set of settings
    /// serves the hand-run test and this host.
    /// </summary>
    public class IcmConfig
    {
        /// <summary>
        /// Gets or sets the ICM base URL including the version prefix, e.g.
        /// <c>https://…/gov/v1.0</c>. Required; the host refuses to start without it.
        /// </summary>
        public Uri? BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets how long to wait for ICM, in seconds. This plus
        /// <see cref="TokenTimeoutSeconds"/> is kept below MyssApi's per-attempt
        /// timeout (30 s) on purpose: if MyssApi gave up first, ICM could still file
        /// the service request after MyssApi had recorded the attempt as failed.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 20;

        /// <summary>
        /// Gets or sets how long to wait for ICM's authorization server, in seconds.
        /// Short, because a cold token cache adds this to every submission's budget.
        /// </summary>
        public int TokenTimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets the ICM user every call acts as, sent as
        /// <c>X-ICM-TrustedUserName</c>. Null sends no header.
        /// </summary>
        public string? TrustedUserName { get; set; }

        /// <summary>Gets or sets the client-credentials details for ICM's authorization server.</summary>
        public IcmAuthConfig Auth { get; set; } = new();

        /// <summary>
        /// Gets a value indicating whether the host can start. Only the non-secret
        /// parts are checked here: the base URL and enough to build the token URL.
        /// The credentials are checked on the first submission instead, so a
        /// checkout without secrets still boots for tests.
        /// </summary>
        public bool IsConfigured =>
            BaseUrl is { IsAbsoluteUri: true } && Auth.ResolveTokenUrl() is not null;
    }
}
