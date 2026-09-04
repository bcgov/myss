namespace Icm.Api.Contracts
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// A successful OAuth 2.0 token response (RFC 6749 §5.1).
    /// </summary>
    /// <remarks>
    /// Only the fields a client-credentials caller can act on are modelled. Refresh tokens
    /// are absent by design: RFC 6749 §4.4.3 says a client-credentials grant must not issue
    /// one, because the client can simply ask again with the credentials it already holds.
    /// </remarks>
    internal class TokenResponse
    {
        /// <summary>Gets or sets the access token itself.</summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        /// <summary>Gets or sets the token type, <c>Bearer</c> for every grant used here.</summary>
        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        /// <summary>
        /// Gets or sets the token's lifetime in seconds, counted from when the
        /// authorization server issued it.
        /// </summary>
        /// <remarks>
        /// RFC 6749 calls this field optional. A response without it is treated as
        /// uncacheable rather than as an error — see <c>OAuthTokenRepository</c>.
        /// </remarks>
        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the scopes actually granted, space-separated. Present only when they
        /// differ from what was asked for.
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }
}
