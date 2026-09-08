namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Everything needed to obtain a token with the client-credentials grant, and the unit
    /// the token cache is keyed on.
    /// </summary>
    /// <remarks>
    /// Passed per call rather than fixed at construction so one service instance can serve
    /// several ICM clients — which is also why the cache has to be keyed rather than being
    /// a single slot.
    /// </remarks>
    public class OAuthClientCredentials
    {
        /// <summary>Gets or sets the full token endpoint URL.</summary>
        public Uri? TokenUrl { get; set; }

        /// <summary>Gets or sets the client identifier.</summary>
        public string? ClientId { get; set; }

        /// <summary>Gets or sets the client secret.</summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the requested scopes. Null or empty asks for none, leaving the
        /// authorization server to apply the client's default.
        /// </summary>
        /// <remarks>
        /// Worth leaving empty on a first attempt. A scope the client is not entitled to is
        /// rejected, and "no scope" is the request most likely to succeed while you are
        /// still establishing that the credentials themselves work.
        /// </remarks>
        public IEnumerable<string>? Scopes { get; set; }

        /// <summary>
        /// Gets the scopes as the single space-separated string RFC 6749 §3.3 puts on the
        /// wire, or null when there are none.
        /// </summary>
        /// <returns>The scope parameter value, or null.</returns>
        public string? GetScopeParameter()
        {
            if (Scopes is null)
            {
                return null;
            }

            string scope = string.Join(
                ' ',
                Scopes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));

            return scope.Length == 0 ? null : scope;
        }
    }
}
