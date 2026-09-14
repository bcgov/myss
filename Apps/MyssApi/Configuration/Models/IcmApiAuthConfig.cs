namespace Myss.Api.Configuration.Models
{
    /// <summary>
    /// Client-credentials settings for calling the ICM middleware, bound from
    /// <c>IcmApi:Auth</c>. The token identifies this API to the middleware; it
    /// has nothing to do with ICM's own credentials, which stay in the middleware.
    /// </summary>
    public class IcmApiAuthConfig
    {
        /// <summary>
        /// Gets or sets the OAuth token endpoint that issues the service token.
        /// </summary>
        public string? TokenEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the client id MyssApi authenticates as.
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
        /// Gets a value indicating whether every value needed to obtain a token is present.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(TokenEndpoint)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
