namespace Myss.Api.Configuration.Models
{
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The client this API authenticates as when it calls another service as
    /// itself, bound from <c>Oidc:ServiceAccount</c>: a confidential Keycloak
    /// client with its service account enabled. It is a different client from
    /// <c>Oidc:ClientId</c>, which names the web client whose citizen tokens this
    /// API accepts. The first consumer is the ICM middleware, but nothing here is
    /// middleware-specific: the same token serves any service that trusts the realm.
    /// The token endpoint derives from <c>Oidc:Authority</c> unless
    /// <see cref="TokenEndpoint"/> overrides it.
    /// </summary>
    public class OidcServiceAccountConfig
    {
        /// <summary>
        /// The configuration section the settings live in.
        /// </summary>
        public const string SectionName = "Oidc:ServiceAccount";

        /// <summary>
        /// The section read before the settings moved here. Honoured for one
        /// release so a deployment can move its values without a flag day.
        /// </summary>
        public const string LegacySectionName = "IcmApi:Auth";

        /// <summary>
        /// Gets or sets the client id this API authenticates as.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client secret. Never commit a value.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the space-separated scopes to request, when the realm needs any.
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// Gets or sets the OAuth token endpoint. Left unset, it is derived from
        /// <c>Oidc:Authority</c> as <c>{Authority}/protocol/openid-connect/token</c>.
        /// </summary>
        public string? TokenEndpoint { get; set; }

        /// <summary>
        /// Gets a value indicating whether the credentials came from the legacy
        /// <see cref="LegacySectionName"/> section rather than <see cref="SectionName"/>.
        /// </summary>
        public bool LegacyKeysUsed { get; private set; }

        /// <summary>
        /// Gets a value indicating whether every value needed to obtain a token is present.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(TokenEndpoint)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);

        /// <summary>
        /// Binds a new instance from the configuration; see <see cref="Bind(IConfiguration, OidcServiceAccountConfig)"/>.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The bound settings.</returns>
        public static OidcServiceAccountConfig Bind(IConfiguration configuration)
        {
            OidcServiceAccountConfig config = new();
            Bind(configuration, config);
            return config;
        }

        /// <summary>
        /// Binds <see cref="SectionName"/> into <paramref name="target"/>, falls back
        /// to <see cref="LegacySectionName"/> when the new section carries no
        /// credentials, and derives the token endpoint from <c>Oidc:Authority</c>
        /// when none is set explicitly.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="target">The instance to fill.</param>
        public static void Bind(IConfiguration configuration, OidcServiceAccountConfig target)
        {
            configuration.GetSection(SectionName).Bind(target);

            if (string.IsNullOrWhiteSpace(target.ClientId) && string.IsNullOrWhiteSpace(target.ClientSecret))
            {
                IConfigurationSection legacy = configuration.GetSection(LegacySectionName);
                string? legacyClientId = legacy["ClientId"];
                string? legacyClientSecret = legacy["ClientSecret"];
                if (!string.IsNullOrWhiteSpace(legacyClientId) || !string.IsNullOrWhiteSpace(legacyClientSecret))
                {
                    target.ClientId = legacyClientId;
                    target.ClientSecret = legacyClientSecret;
                    target.Scope ??= legacy["Scope"];
                    target.TokenEndpoint ??= legacy["TokenEndpoint"];
                    target.LegacyKeysUsed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(target.TokenEndpoint))
            {
                string? authority = configuration["Oidc:Authority"];
                if (!string.IsNullOrWhiteSpace(authority))
                {
                    target.TokenEndpoint = authority.TrimEnd('/') + "/protocol/openid-connect/token";
                }
            }
        }
    }
}
