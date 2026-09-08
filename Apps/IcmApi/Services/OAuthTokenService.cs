namespace Icm.Api.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Caches client-credentials access tokens for as long as they are valid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Caching is all this does. Getting a token is
    /// <see cref="IOAuthTokenRepository"/>'s job, which is what makes this class testable
    /// without a transport and what keeps the two concerns separable.
    /// </para>
    /// <para>
    /// <b>Cache key.</b> One entry per token endpoint + client id + requested scope. The
    /// endpoint and client id are the obvious part: two clients must never see each other's
    /// token. The scope is in the key because a token is issued <i>for</i> the scopes that
    /// were asked for — serving a narrowly-scoped cached token to a caller that asked for
    /// more would fail later, at the resource server, as an authorization error that says
    /// nothing about where it came from.
    /// </para>
    /// <para>
    /// <b>The client secret is deliberately not in the key.</b> Cache keys end up in logs
    /// and dumps. Leaving it out means a secret rotation keeps serving the token issued
    /// under the old secret until it expires, which is correct anyway: rotating a secret
    /// does not revoke tokens already issued.
    /// </para>
    /// <para>
    /// <b>One request per key at a time.</b> Without the gate below, a burst of callers on
    /// a cold cache would each hit the authorization server. The double check around it is
    /// not redundant — the fast path avoids the lock entirely while the token is good, and
    /// the second check catches the callers that queued behind whoever refreshed it.
    /// </para>
    /// <para>
    /// Thread-safe, and intended to be registered as a singleton. Registering it
    /// per-request would give every request its own empty cache, which is the same as no
    /// cache.
    /// </para>
    /// </remarks>
    public sealed class OAuthTokenService : IOAuthTokenService, IDisposable
    {
        /// <summary>
        /// How long before actual expiry a token is treated as expired. Covers the flight
        /// time of the request the token is about to be used on, plus clock skew between
        /// here and the authorization server.
        /// </summary>
        public static readonly TimeSpan DefaultExpiryBuffer = TimeSpan.FromSeconds(30);

        private readonly ConcurrentDictionary<string, CachedToken> _tokens = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshGates = new(StringComparer.Ordinal);
        private readonly IOAuthTokenRepository _repository;
        private readonly TimeSpan _expiryBuffer;
        private readonly TimeProvider _timeProvider;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthTokenService"/> class.
        /// </summary>
        /// <param name="repository">The source of tokens.</param>
        /// <param name="expiryBuffer">
        /// How early a token is considered expired. Null uses
        /// <see cref="DefaultExpiryBuffer"/>.
        /// </param>
        /// <param name="timeProvider">
        /// The clock. Null uses <see cref="TimeProvider.System"/>. Injectable so expiry can
        /// be tested without waiting for it.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="repository"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="expiryBuffer"/> is negative.
        /// </exception>
        public OAuthTokenService(
            IOAuthTokenRepository repository,
            TimeSpan? expiryBuffer = null,
            TimeProvider? timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(repository);

            if (expiryBuffer is { } buffer && buffer < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expiryBuffer), expiryBuffer, "The expiry buffer cannot be negative.");
            }

            _repository = repository;
            _expiryBuffer = expiryBuffer ?? DefaultExpiryBuffer;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <inheritdoc/>
        public async Task<string> GetTokenAsync(
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

            // Checked here, not left to the repository: the secret is not part of the
            // cache key, so once the key is warm a caller with a blank secret would be
            // handed the cached token and never reach the repository's own check.
            if (string.IsNullOrWhiteSpace(credentials.ClientSecret))
            {
                throw new ArgumentException("A client secret is required.", nameof(credentials));
            }

            string key = BuildCacheKey(tokenUrl, credentials.ClientId, credentials.GetScopeParameter());

            if (TryGetCachedToken(key, out string? cached))
            {
                return cached;
            }

            SemaphoreSlim gate = _refreshGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Whoever held the gate may have just refreshed this key.
                if (TryGetCachedToken(key, out cached))
                {
                    return cached;
                }

                return await RefreshAsync(key, credentials, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Discards every cached token. Intended for a credential rotation or a test; there
        /// is no need to call it to pick up an expiry.
        /// </summary>
        public void Clear() => _tokens.Clear();

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _tokens.Clear();

            foreach (SemaphoreSlim gate in _refreshGates.Values)
            {
                gate.Dispose();
            }

            _refreshGates.Clear();
        }

        /// <summary>
        /// Builds the cache key.
        /// </summary>
        /// <remarks>
        /// <see cref="Uri.AbsoluteUri"/> normalises the host's case, a default port and any
        /// dot segments, so those spellings of one endpoint share an entry. It does
        /// <b>not</b> normalise a trailing slash — <c>/token</c> and <c>/token/</c> are
        /// different keys, and are left that way deliberately: they are different URLs, and
        /// an authorization server is entitled to treat them differently. The cost of being
        /// wrong about that is one extra token request, against the cost of silently serving
        /// a token minted by an endpoint the caller did not ask for.
        /// </remarks>
        private static string BuildCacheKey(Uri tokenUrl, string clientId, string? scope) =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{tokenUrl.AbsoluteUri}\n{clientId}\n{scope ?? string.Empty}");

        private bool TryGetCachedToken(
            string key,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? token)
        {
            if (_tokens.TryGetValue(key, out CachedToken? cached)
                && cached.ExpiresAt > _timeProvider.GetUtcNow())
            {
                token = cached.AccessToken;
                return true;
            }

            token = null;
            return false;
        }

        private async Task<string> RefreshAsync(
            string key,
            OAuthClientCredentials credentials,
            CancellationToken cancellationToken)
        {
            // Read the clock before the call, not after: a token's lifetime starts when the
            // server issues it, so counting from the reply would over-state it by the round
            // trip.
            DateTimeOffset requestedAt = _timeProvider.GetUtcNow();

            AccessToken token = await _repository
                .GetTokenAsync(credentials, cancellationToken)
                .ConfigureAwait(false);

            // A token good for less than the buffer is returned but not cached: caching it
            // would hand the next caller something already treated as expired, and a
            // response with no lifetime at all says nothing about how long it lasts.
            if (token.Lifetime > _expiryBuffer)
            {
                _tokens[key] = new CachedToken(
                    token.Value, requestedAt + token.Lifetime - _expiryBuffer);
            }
            else
            {
                _tokens.TryRemove(key, out _);
            }

            return token.Value;
        }

        /// <summary>A token and the moment this service stops handing it out.</summary>
        private sealed class CachedToken
        {
            public CachedToken(string accessToken, DateTimeOffset expiresAt)
            {
                AccessToken = accessToken;
                ExpiresAt = expiresAt;
            }

            public string AccessToken { get; }

            public DateTimeOffset ExpiresAt { get; }
        }
    }
}
