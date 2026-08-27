namespace Icm.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// Supplies access tokens for the client-credentials grant, reusing one until it is
    /// close to expiring.
    /// </summary>
    /// <remarks>
    /// This is the exposed surface;
    /// <see cref="Icm.Api.Repositories.IOAuthTokenRepository"/> is the data access beneath
    /// it. Callers should hold the service, not the token — asking again is cheap (it is a
    /// dictionary lookup while the cached token is good) and is the only way a caller picks
    /// up a renewal.
    /// </remarks>
    public interface IOAuthTokenService
    {
        /// <summary>
        /// Gets a valid access token for the given credentials, from cache when one is
        /// still good and from the authorization server otherwise.
        /// </summary>
        /// <param name="credentials">The token endpoint, client id, secret and scopes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The access token.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="credentials"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// The credentials are missing a token URL, a client id or a client secret.
        /// </exception>
        /// <exception cref="Refit.ApiException">
        /// The authorization server rejected the request.
        /// </exception>
        /// <exception cref="Icm.Api.Repositories.OAuthTokenException">
        /// The authorization server answered successfully but without a usable token.
        /// </exception>
        Task<string> GetTokenAsync(
            OAuthClientCredentials credentials,
            CancellationToken cancellationToken = default);
    }
}
