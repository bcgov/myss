namespace Icm.Api.Repositories
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// Exchanges client credentials for an access token.
    /// </summary>
    /// <remarks>
    /// The data-access half of getting a token: one call out, no caching, no memory of the
    /// last one. <see cref="Services.IOAuthTokenService"/> is the half that remembers, and
    /// is what callers should hold — going through here directly means a round trip to the
    /// authorization server on every single request.
    /// </remarks>
    public interface IOAuthTokenRepository
    {
        /// <summary>Requests a token with the client-credentials grant.</summary>
        /// <param name="credentials">The token endpoint, client id, secret and scopes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The token and its lifetime.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="credentials"/> is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// The credentials are missing a token URL, a client id or a client secret.
        /// </exception>
        /// <exception cref="Refit.ApiException">The authorization server rejected the request.</exception>
        /// <exception cref="OAuthTokenException">
        /// The authorization server answered successfully but without a usable token.
        /// </exception>
        Task<AccessToken> GetTokenAsync(
            OAuthClientCredentials credentials,
            CancellationToken cancellationToken = default);
    }
}
