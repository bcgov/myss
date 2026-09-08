namespace Icm.Api.Contracts
{
    using Refit;

    /// <summary>
    /// The form-encoded body of a client-credentials token request (RFC 6749 §4.4.2).
    /// </summary>
    /// <remarks>
    /// This carries a client secret. It is sent as a form body rather than in the URL for
    /// exactly that reason, and it must never be logged or put in an exception message.
    /// </remarks>
    internal class TokenRequest
    {
        /// <summary>
        /// Gets the grant type. Always <c>client_credentials</c>. An instance property with
        /// a fixed value rather than a constant, because Refit form-encodes instance
        /// properties and would not see a static one.
        /// </summary>
        [AliasAs("grant_type")]
        public string GrantType { get; } = "client_credentials";

        /// <summary>Gets or sets the client identifier.</summary>
        [AliasAs("client_id")]
        public string? ClientId { get; set; }

        /// <summary>Gets or sets the client secret.</summary>
        [AliasAs("client_secret")]
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the requested scopes, space-separated. Omitted entirely when no
        /// scopes were asked for, so the authorization server applies the client's default.
        /// </summary>
        [AliasAs("scope")]
        public string? Scope { get; set; }
    }
}
