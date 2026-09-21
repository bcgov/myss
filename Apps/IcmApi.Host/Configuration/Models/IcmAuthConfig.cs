namespace Icm.Api.Host.Configuration.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The client-credentials grant this host presents to ICM's authorization
    /// server, bound from <c>Icm:Auth</c>.
    /// </summary>
    /// <remarks>
    /// The token endpoint is composed from <see cref="BaseUrl"/> and
    /// <see cref="Realm"/> unless <see cref="TokenUrl"/> overrides it, the same
    /// arrangement IcmApi.Console documents: the realm is the setting most likely
    /// to be wrong, and it is easier to see as a word than as a path segment.
    /// </remarks>
    public class IcmAuthConfig
    {
        /// <summary>
        /// Gets or sets the Keycloak base URL, including <c>/auth</c> on deployments
        /// that still use it (BC Gov's loginproxy does).
        /// </summary>
        public Uri? BaseUrl { get; set; }

        /// <summary>Gets or sets the realm the ICM client is registered in.</summary>
        public string? Realm { get; set; }

        /// <summary>
        /// Gets or sets the full token endpoint, overriding <see cref="BaseUrl"/> and
        /// <see cref="Realm"/> when set.
        /// </summary>
        public Uri? TokenUrl { get; set; }

        /// <summary>Gets or sets the client identifier.</summary>
        public string? ClientId { get; set; }

        /// <summary>Gets or sets the client secret. Never in source control.</summary>
        public string? ClientSecret { get; set; }

        /// <summary>Gets the scopes to request. Empty asks for none.</summary>
        public IList<string> Scopes { get; } = [];

        /// <summary>Gets a value indicating whether both halves of the credential are present.</summary>
        public bool HasCredentials =>
            !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

        /// <summary>
        /// Resolves the token endpoint: <see cref="TokenUrl"/> when set, otherwise
        /// <c>{BaseUrl}/realms/{Realm}/protocol/openid-connect/token</c>.
        /// </summary>
        /// <returns>The endpoint, or null when there is not enough configuration to build one.</returns>
        public Uri? ResolveTokenUrl()
        {
            if (TokenUrl is { IsAbsoluteUri: true })
            {
                return TokenUrl;
            }

            if (BaseUrl is not { IsAbsoluteUri: true } || string.IsNullOrWhiteSpace(Realm))
            {
                return null;
            }

            return new Uri(
                $"{BaseUrl.AbsoluteUri.TrimEnd('/')}/realms/{Realm.Trim().Trim('/')}/protocol/openid-connect/token");
        }
    }
}
