namespace Icm.Api.Tests.Repositories
{
    using System.Net;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// The token repository over a canned authorization server.
    /// </summary>
    public class OAuthTokenRepositoryTests
    {
        private static readonly Uri TokenUrl = new("https://login.example.gov.bc.ca/realms/a/token");

        private static OAuthClientCredentials Credentials() => new()
        {
            TokenUrl = TokenUrl,
            ClientId = "myss-icm",
            ClientSecret = "s3cr3t",
        };

        private static (OAuthTokenRepository Repository, RecordingHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = """{"access_token":"abc","token_type":"Bearer","expires_in":300}""")
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = TokenUrl };
            return (new OAuthTokenRepository(_ => httpClient), handler);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsTheTokenAndItsLifetime()
        {
            (OAuthTokenRepository repository, _) = Create();

            AccessToken token = await repository.GetTokenAsync(Credentials());

            Assert.Equal("abc", token.Value);
            Assert.Equal(TimeSpan.FromSeconds(300), token.Lifetime);
        }

        [Fact]
        public async Task GetTokenAsync_ReportsNoLifetimeWhenTheServerGaveNone()
        {
            // RFC 6749 makes expires_in optional. Zero is what tells the service it cannot
            // safely cache this one.
            (OAuthTokenRepository repository, _) = Create(
                responseJson: """{"access_token":"abc","token_type":"Bearer"}""");

            AccessToken token = await repository.GetTokenAsync(Credentials());

            Assert.Equal(TimeSpan.Zero, token.Lifetime);
        }

        [Fact]
        public async Task GetTokenAsync_ThrowsWhenTheResponseCarriesNoToken()
        {
            (OAuthTokenRepository repository, _) = Create(
                responseJson: """{"token_type":"Bearer","expires_in":300}""");

            OAuthTokenException exception = await Assert.ThrowsAsync<OAuthTokenException>(
                () => repository.GetTokenAsync(Credentials()));

            // Actionable about where, silent about what was sent.
            Assert.Contains(TokenUrl.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("s3cr3t", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetTokenAsync_TreatsAWhitespaceTokenAsMissing()
        {
            // Whitespace would only fail later, inside AccessToken's own guard, as an
            // ArgumentException that no longer names the endpoint that misbehaved.
            (OAuthTokenRepository repository, _) = Create(
                responseJson: """{"access_token":"   ","token_type":"Bearer","expires_in":300}""");

            await Assert.ThrowsAsync<OAuthTokenException>(
                () => repository.GetTokenAsync(Credentials()));
        }

        [Fact]
        public async Task GetTokenAsync_RejectsATokenTypeThatIsNotBearer()
        {
            // Every caller sends the token as `Authorization: Bearer …`; a token the
            // server says is another type cannot travel in that scheme.
            (OAuthTokenRepository repository, _) = Create(
                responseJson: """{"access_token":"abc","token_type":"DPoP","expires_in":300}""");

            OAuthTokenException exception = await Assert.ThrowsAsync<OAuthTokenException>(
                () => repository.GetTokenAsync(Credentials()));

            Assert.Contains("DPoP", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetTokenAsync_ToleratesAMissingTokenTypeAsBearer()
        {
            // RFC 6749 requires token_type, but rejecting its omission would fail servers
            // whose tokens work fine; bearer casing also varies in the wild.
            (OAuthTokenRepository repository, _) = Create(
                responseJson: """{"access_token":"abc","expires_in":300}""");
            Assert.Equal("abc", (await repository.GetTokenAsync(Credentials())).Value);

            (OAuthTokenRepository lowerCase, _) = Create(
                responseJson: """{"access_token":"abc","token_type":"bearer","expires_in":300}""");
            Assert.Equal("abc", (await lowerCase.GetTokenAsync(Credentials())).Value);
        }

        [Fact]
        public async Task GetTokenAsync_ThrowsWhenTheServerRejectsTheRequest()
        {
            (OAuthTokenRepository repository, _) = Create(
                HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""");

            ApiException exception = await Assert.ThrowsAsync<ApiException>(
                () => repository.GetTokenAsync(Credentials()));

            Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        }

        [Fact]
        public async Task GetTokenAsync_SendsTheClientCredentialsGrantAndTheScopes()
        {
            (OAuthTokenRepository repository, RecordingHttpMessageHandler handler) = Create();

            await repository.GetTokenAsync(new OAuthClientCredentials
            {
                TokenUrl = TokenUrl,
                ClientId = "myss-icm",
                ClientSecret = "s3cr3t",
                Scopes = ["read", "write"],
            });

            Assert.Equal(
                "application/x-www-form-urlencoded",
                handler.Request!.Content!.Headers.ContentType!.MediaType);

            // client_secret_post (RFC 6749 §2.3.1): the credentials go in the body, and
            // there is no Authorization header on a token request.
            Assert.Null(handler.Request.Headers.Authorization);

            string body = Uri.UnescapeDataString(handler.RequestBody!).Replace('+', ' ');
            Assert.Contains("grant_type=client_credentials", body, StringComparison.Ordinal);
            Assert.Contains("client_id=myss-icm", body, StringComparison.Ordinal);
            Assert.Contains("client_secret=s3cr3t", body, StringComparison.Ordinal);
            Assert.Contains("scope=read write", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetTokenAsync_RejectsCredentialsItCannotUse()
        {
            (OAuthTokenRepository repository, RecordingHttpMessageHandler handler) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(() => repository.GetTokenAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(
                () => repository.GetTokenAsync(new OAuthClientCredentials { ClientId = "a", ClientSecret = "b" }));
            await Assert.ThrowsAsync<ArgumentException>(
                () => repository.GetTokenAsync(new OAuthClientCredentials { TokenUrl = TokenUrl, ClientSecret = "b" }));
            await Assert.ThrowsAsync<ArgumentException>(
                () => repository.GetTokenAsync(new OAuthClientCredentials { TokenUrl = TokenUrl, ClientId = "a" }));

            Assert.Null(handler.Request);
        }
    }
}
