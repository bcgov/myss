namespace Icm.Api.Host.Configuration.Models
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Who may call this middleware, bound from the <c>Oidc</c> section. Callers
    /// present a bearer token from the shared standard realm; the token must be
    /// issued to one of the <see cref="AllowedClients"/>.
    /// </summary>
    public class OidcConfig
    {
        /// <summary>Gets or sets the issuer whose signing keys validate inbound tokens.</summary>
        public string? Authority { get; set; }

        /// <summary>
        /// Gets or sets the expected <c>aud</c> claim. Unset disables audience
        /// validation and leaves the <see cref="AllowedClients"/> check as the guard,
        /// which is what a client-credentials token from the standard realm needs:
        /// its audience is the requesting client, not this host.
        /// </summary>
        public string? Audience { get; set; }

        /// <summary>
        /// Gets the client ids (the token's <c>azp</c>) allowed to call. Required
        /// when real authentication is on: an empty list would let any client of the
        /// realm file bus pass requests.
        /// </summary>
        public IList<string> AllowedClients { get; } = [];

        /// <summary>Gets a value indicating whether real authentication can be configured.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Authority)
            && AllowedClients.Any(client => !string.IsNullOrWhiteSpace(client));
    }
}
