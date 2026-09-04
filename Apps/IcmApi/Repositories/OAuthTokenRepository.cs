namespace Icm.Api.Repositories
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Refit;

    /// <summary>
    /// <see cref="IOAuthTokenRepository"/> over an OAuth 2.0 token endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Holds one transport per token endpoint, because the endpoint comes from the client's
    /// base address — token URLs differ per realm and per environment, and a Refit route is
    /// fixed at compile time. Endpoints are few and long-lived, so caching them is a
    /// dictionary rather than anything cleverer.
    /// </para>
    /// <para>
    /// Nothing here is logged. The request body carries a client secret, which is also why
    /// <see cref="IcmRefitSettings"/> leaves Refit's request-content capture off — with it
    /// on, a failed token request would put the secret inside the exception.
    /// </para>
    /// </remarks>
    public sealed class OAuthTokenRepository : IOAuthTokenRepository, IDisposable
    {
        private readonly ConcurrentDictionary<Uri, Lazy<IOAuthTokenApi>> _apis = new();
        private readonly ConcurrentDictionary<Uri, Lazy<HttpClient>> _ownedClients = new();
        private readonly Func<Uri, IOAuthTokenApi> _apiFactory;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthTokenRepository"/> class that
        /// creates and owns one <see cref="HttpClient"/> per token endpoint.
        /// </summary>
        public OAuthTokenRepository()
            : this((Func<Uri, HttpClient>?)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthTokenRepository"/> class.
        /// </summary>
        /// <param name="httpClientFactory">
        /// Supplies a client for a token endpoint — hand it one backed by
        /// <c>IHttpClientFactory</c> so the host's handler pooling applies. Null makes this
        /// repository create its own and dispose them with itself; a client from a supplied
        /// factory is never disposed here.
        /// </param>
        public OAuthTokenRepository(Func<Uri, HttpClient>? httpClientFactory)
        {
            _apiFactory = httpClientFactory is null
                ? CreateOwnedApi
                : url => RestService.For<IOAuthTokenApi>(
                    httpClientFactory(url), IcmRefitSettings.Create());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthTokenRepository"/> class over a
        /// specific transport. For tests.
        /// </summary>
        /// <param name="apiFactory">Builds the transport for a token endpoint.</param>
        internal OAuthTokenRepository(Func<Uri, IOAuthTokenApi> apiFactory) =>
            _apiFactory = apiFactory;

        /// <inheritdoc/>
        public async Task<AccessToken> GetTokenAsync(
            OAuthClientCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(credentials);

            Uri tokenUrl = credentials.TokenUrl
                ?? throw new ArgumentException("A token URL is required.", nameof(credentials));

            if (string.IsNullOrWhiteSpace(credentials.ClientId))
            {
                throw new ArgumentException("A client id is required.", nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ClientSecret))
            {
                throw new ArgumentException("A client secret is required.", nameof(credentials));
            }

            IOAuthTokenApi api = _apis
                .GetOrAdd(tokenUrl, url => new Lazy<IOAuthTokenApi>(() => _apiFactory(url)))
                .Value;

            TokenResponse response = await api.GetTokenAsync(
                new TokenRequest
                {
                    ClientId = credentials.ClientId,
                    ClientSecret = credentials.ClientSecret,
                    Scope = credentials.GetScopeParameter(),
                },
                cancellationToken).ConfigureAwait(false);

            // Whitespace counts as missing: it would only fail later, inside AccessToken's
            // own guard, as an ArgumentException that hides which endpoint misbehaved.
            if (response is null || string.IsNullOrWhiteSpace(response.AccessToken))
            {
                // Say where, so the message is actionable — and nothing about what was sent.
                throw new OAuthTokenException(
                    $"The token endpoint at '{tokenUrl}' returned a successful response with no access token.");
            }

            // Every caller sends this token as `Authorization: Bearer …`. A server that
            // says the token is some other type (DPoP, MAC) is describing a token that
            // scheme cannot carry — better to say so here than surface it later as an
            // inexplicable 401 from ICM. An absent token_type is tolerated as Bearer:
            // RFC 6749 requires the field, but rejecting its omission would fail servers
            // whose tokens work fine.
            if (response.TokenType is { Length: > 0 } tokenType
                && !string.Equals(tokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                throw new OAuthTokenException(
                    $"The token endpoint at '{tokenUrl}' issued a '{tokenType}' token; "
                    + "only Bearer tokens can be used here.");
            }

            string accessToken = response.AccessToken;

            TimeSpan lifetime = response.ExpiresIn is { } seconds && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.Zero;

            return new AccessToken(accessToken, lifetime);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (Lazy<HttpClient> client in _ownedClients.Values)
            {
                if (client.IsValueCreated)
                {
                    client.Value.Dispose();
                }
            }

            _ownedClients.Clear();
            _apis.Clear();
        }

        private IOAuthTokenApi CreateOwnedApi(Uri tokenUrl)
        {
            HttpClient httpClient = _ownedClients
                .GetOrAdd(tokenUrl, url => new Lazy<HttpClient>(() => new HttpClient { BaseAddress = url }))
                .Value;

            return RestService.For<IOAuthTokenApi>(httpClient, IcmRefitSettings.Create());
        }
    }
}
