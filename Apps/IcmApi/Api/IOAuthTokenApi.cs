namespace Icm.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Refit;

    /// <summary>
    /// The OAuth 2.0 token endpoint, called with the client-credentials grant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Internal.</b> <see cref="Repositories.IOAuthTokenRepository"/> is the published
    /// boundary and <see cref="Services.IOAuthTokenService"/> is what callers should hold —
    /// it caches, and reaching past it means a round trip on every request.
    /// </para>
    /// <para>
    /// <b>This one throws, unlike <see cref="IServiceRequestApi"/>.</b> The Service Request
    /// API returns <see cref="IApiResponse{T}"/> because Siebel answers "found nothing" with
    /// a non-2xx status that is not a failure. Here there is no such case: either a token
    /// comes back or the request failed, so a non-success status is an
    /// <see cref="ApiException"/> and the caller gets a token or an exception, never a null.
    /// </para>
    /// <para>
    /// The URL is the client's <see cref="System.Net.Http.HttpClient.BaseAddress"/> — token
    /// endpoints differ per realm and per environment, and Refit binds its route at compile
    /// time, so the address is the only place the endpoint can vary at run time.
    /// </para>
    /// </remarks>
    [Headers("Accept: application/json")]
    internal interface IOAuthTokenApi
    {
        /// <summary>
        /// Exchanges client credentials for an access token.
        /// </summary>
        /// <param name="request">
        /// The grant type, client id, secret and scopes, form-url-encoded per RFC 6749
        /// §4.4.2 — the <c>client_secret_post</c> method of §2.3.1.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The token response.</returns>
        /// <exception cref="ApiException">
        /// The authorization server rejected the request — bad credentials, an unknown
        /// client, the wrong realm, or a scope the client may not ask for.
        /// </exception>
        [Post("")]
        Task<TokenResponse> GetTokenAsync(
            [Body(BodySerializationMethod.UrlEncoded)] TokenRequest request,
            CancellationToken cancellationToken = default);
    }
}
