namespace Icm.Api.Tests.Services
{
    using System.Net;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// The caching behaviour of <see cref="OAuthTokenService"/>. Everything here turns on
    /// how many times the endpoint was actually called, which is the only externally visible
    /// difference between a cache that works and one that does not.
    /// </summary>
    public class OAuthTokenServiceTests
    {
        private static readonly DateTimeOffset Start = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
        private static readonly Uri TokenUrl = new("https://login.example.gov.bc.ca/realms/a/token");
        private static readonly Uri OtherTokenUrl = new("https://login.example.gov.bc.ca/realms/b/token");

        private static OAuthClientCredentials Credentials(
            Uri? tokenUrl = null,
            string clientId = "myss-icm",
            string clientSecret = "s3cr3t",
            params string[] scopes) =>
            new()
            {
                TokenUrl = tokenUrl ?? TokenUrl,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Scopes = scopes.Length == 0 ? null : scopes,
            };

        [Fact]
        public async Task GetTokenAsync_ReturnsTheAccessToken()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            string token = await service.GetTokenAsync(Credentials());

            Assert.Equal("token-1", token);
        }

        [Fact]
        public async Task GetTokenAsync_SendsTheCredentialsAndScopesItWasGiven()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            await service.GetTokenAsync(Credentials(scopes: ["read", "write"]));

            (Uri url, OAuthClientCredentials request) = Assert.Single(endpoint.Calls);
            Assert.Equal(TokenUrl, url);
            Assert.Equal("myss-icm", request.ClientId);
            Assert.Equal("s3cr3t", request.ClientSecret);
            Assert.Equal("read write", request.GetScopeParameter());
        }

        [Fact]
        public async Task GetTokenAsync_ReusesTheCachedTokenWhileItIsValid()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            string first = await service.GetTokenAsync(Credentials());
            string second = await service.GetTokenAsync(Credentials());
            string third = await service.GetTokenAsync(Credentials());

            Assert.Equal(first, second);
            Assert.Equal(first, third);
            Assert.Equal(1, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_FetchesAgainOnceTheTokenExpires()
        {
            FakeTokenRepository endpoint = new() { ExpiresIn = 300 };
            FakeTimeProvider clock = new(Start);
            using OAuthTokenService service = new(
                endpoint, TimeSpan.FromSeconds(30), clock);

            Assert.Equal("token-1", await service.GetTokenAsync(Credentials()));

            // One second short of (300 - 30): still the cached token.
            clock.Advance(TimeSpan.FromSeconds(269));
            Assert.Equal("token-1", await service.GetTokenAsync(Credentials()));
            Assert.Equal(1, endpoint.CallCount);

            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal("token-2", await service.GetTokenAsync(Credentials()));
            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_RetiresTheTokenEarlyByTheExpiryBuffer()
        {
            // A token with 300 seconds left is dropped at 240 with a 60 second buffer, so
            // it is never handed to a caller that would use it in its last minute.
            FakeTokenRepository endpoint = new() { ExpiresIn = 300 };
            FakeTimeProvider clock = new(Start);
            using OAuthTokenService service = new(
                endpoint, TimeSpan.FromSeconds(60), clock);

            await service.GetTokenAsync(Credentials());
            clock.Advance(TimeSpan.FromSeconds(241));
            await service.GetTokenAsync(Credentials());

            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_CachesSeparatelyPerClientId()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            string first = await service.GetTokenAsync(Credentials(clientId: "client-a"));
            string second = await service.GetTokenAsync(Credentials(clientId: "client-b"));

            Assert.NotEqual(first, second);
            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_CachesSeparatelyPerTokenUrl()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            string first = await service.GetTokenAsync(Credentials());
            string second = await service.GetTokenAsync(Credentials(tokenUrl: OtherTokenUrl));

            Assert.NotEqual(first, second);
            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_CachesSeparatelyPerScope()
        {
            // A token issued for "read" must not be handed to a caller that asked for
            // "read write" — that failure would otherwise surface at the resource server.
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            string read = await service.GetTokenAsync(Credentials(scopes: ["read"]));
            string readWrite = await service.GetTokenAsync(Credentials(scopes: ["read", "write"]));

            Assert.NotEqual(read, readWrite);
            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_TreatsTheSameScopesInADifferentOrderAsADifferentKey()
        {
            // Documenting rather than endorsing: the scope string is used verbatim, so
            // callers should keep scope order stable to get cache hits.
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            await service.GetTokenAsync(Credentials(scopes: ["read", "write"]));
            await service.GetTokenAsync(Credentials(scopes: ["write", "read"]));

            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_IgnoresTheSecretWhenCaching()
        {
            // Deliberate: secrets do not belong in cache keys, and rotating one does not
            // revoke the token already issued under the old one.
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            string first = await service.GetTokenAsync(Credentials(clientSecret: "old"));
            string second = await service.GetTokenAsync(Credentials(clientSecret: "new"));

            Assert.Equal(first, second);
            Assert.Equal(1, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_MakesOneRequestWhenCallersArriveOnAColdCache()
        {
            // Without the single-flight gate every caller that arrives before the first
            // response lands would ask the authorization server for its own token.
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            FakeTokenRepository endpoint = new()
            {
                BeforeResponding = () =>
                {
                    entered.TrySetResult();
                    return release.Task;
                },
            };
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            Task<string> first = Task.Run(() => service.GetTokenAsync(Credentials()));
            await entered.Task;

            Task<string>[] latecomers = [.. Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => service.GetTokenAsync(Credentials())))];

            // Give the latecomers time to reach the gate while the first call is still in
            // flight — that is the window this test exists to cover.
            await Task.Delay(100);
            Assert.Equal(1, endpoint.CallCount);
            Assert.All(latecomers, caller => Assert.False(caller.IsCompleted));

            release.SetResult();
            string[] tokens = [await first, .. await Task.WhenAll(latecomers)];

            Assert.Equal(1, endpoint.CallCount);
            Assert.All(tokens, token => Assert.Equal("token-1", token));
        }

        [Fact]
        public async Task GetTokenAsync_LetsARepositoryFailureThrough()
        {
            // Detecting a tokenless response is the repository's job; the service must not
            // swallow it or turn it into something else.
            FakeTokenRepository endpoint = new()
            {
                Throws = new OAuthTokenException("no access token"),
            };
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            OAuthTokenException exception = await Assert.ThrowsAsync<OAuthTokenException>(
                () => service.GetTokenAsync(Credentials()));

            Assert.Equal("no access token", exception.Message);
        }

        [Fact]
        public async Task GetTokenAsync_LetsAFailurePropagateAndDoesNotCacheIt()
        {
            // A cached failure would be worse than no cache: every later call would fail
            // without ever asking again.
            FakeTokenRepository endpoint = new()
            {
                Throws = await ApiException.Create(
                    new HttpRequestMessage(HttpMethod.Post, TokenUrl),
                    HttpMethod.Post,
                    new HttpResponseMessage(HttpStatusCode.Unauthorized),
                    new RefitSettings()),
            };
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            await Assert.ThrowsAsync<ApiException>(() => service.GetTokenAsync(Credentials()));

            endpoint.Throws = null;
            Assert.Equal("token-1", await service.GetTokenAsync(Credentials()));
            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_DoesNotCacheATokenShorterLivedThanTheBuffer()
        {
            // Caching it would store something already considered expired.
            FakeTokenRepository endpoint = new() { ExpiresIn = 10 };
            using OAuthTokenService service = new(
                endpoint, TimeSpan.FromSeconds(30), new FakeTimeProvider(Start));

            Assert.Equal("token-1", await service.GetTokenAsync(Credentials()));
            Assert.Equal("token-2", await service.GetTokenAsync(Credentials()));
            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_DoesNotCacheAResponseWithNoExpiry()
        {
            FakeTokenRepository endpoint = new() { ExpiresIn = null };
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            await service.GetTokenAsync(Credentials());
            await service.GetTokenAsync(Credentials());

            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task Clear_DropsTheCachedTokens()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            await service.GetTokenAsync(Credentials());
            service.Clear();
            await service.GetTokenAsync(Credentials());

            Assert.Equal(2, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_RejectsCredentialsItCannotUse()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetTokenAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.GetTokenAsync(new OAuthClientCredentials { ClientId = "a", ClientSecret = "b" }));
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.GetTokenAsync(new OAuthClientCredentials { TokenUrl = TokenUrl, ClientSecret = "b" }));
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.GetTokenAsync(new OAuthClientCredentials { TokenUrl = TokenUrl, ClientId = "a" }));

            Assert.Equal(0, endpoint.CallCount);
        }

        [Fact]
        public async Task GetTokenAsync_RejectsABlankSecretEvenWhenTheCacheIsWarm()
        {
            // The secret is deliberately not part of the cache key, so this rejection has
            // to happen before the cache is consulted — otherwise a caller with no secret
            // at all would be quietly handed the token minted under someone else's.
            FakeTokenRepository endpoint = new();
            using OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));

            await service.GetTokenAsync(Credentials());

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.GetTokenAsync(Credentials(clientSecret: "  ")));
        }

        [Fact]
        public async Task GetTokenAsync_ThrowsAfterDisposal()
        {
            FakeTokenRepository endpoint = new();
            OAuthTokenService service = new(endpoint, timeProvider: new FakeTimeProvider(Start));
            service.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => service.GetTokenAsync(Credentials()));
        }
    }
}
