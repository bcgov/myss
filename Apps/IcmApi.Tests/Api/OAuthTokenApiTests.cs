namespace Icm.Api.Tests.Api
{
    using System.Net;
    using Icm.Api;
    using Icm.Api.Contracts;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// Checks the token request that reaches the authorization server. The endpoint comes
    /// from the client's base address rather than from the route, which is unusual enough
    /// to be worth pinning down.
    /// </summary>
    public class OAuthTokenApiTests
    {
        private static readonly Uri TokenUrl =
            new("https://login.example.gov.bc.ca/auth/realms/comsvcauth/protocol/openid-connect/token");

        private static (IOAuthTokenApi Api, RecordingHttpMessageHandler Handler) CreateApi(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = """{"access_token":"abc","token_type":"Bearer","expires_in":300}""")
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = TokenUrl };
            return (RestService.For<IOAuthTokenApi>(httpClient, IcmRefitSettings.Create()), handler);
        }

        [Fact]
        public async Task GetTokenAsync_PostsToTheBaseAddressUnchanged()
        {
            (IOAuthTokenApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetTokenAsync(new TokenRequest());

            Assert.Equal(HttpMethod.Post, handler.Request!.Method);
            Assert.Equal(TokenUrl, handler.Request.RequestUri);
        }

        [Fact]
        public async Task GetTokenAsync_SendsTheGrantFormUrlEncoded()
        {
            (IOAuthTokenApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetTokenAsync(new TokenRequest
            {
                ClientId = "myss-icm",
                ClientSecret = "s3cr3t",
                Scope = "read write",
            });

            Assert.Equal(
                "application/x-www-form-urlencoded",
                handler.Request!.Content!.Headers.ContentType!.MediaType);

            string body = Uri.UnescapeDataString(handler.RequestBody!).Replace('+', ' ');
            Assert.Contains("grant_type=client_credentials", body, StringComparison.Ordinal);
            Assert.Contains("client_id=myss-icm", body, StringComparison.Ordinal);
            Assert.Contains("client_secret=s3cr3t", body, StringComparison.Ordinal);
            Assert.Contains("scope=read write", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetTokenAsync_OmitsScopeWhenNoneWasAskedFor()
        {
            (IOAuthTokenApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetTokenAsync(new TokenRequest { ClientId = "myss-icm" });

            Assert.DoesNotContain("scope=", handler.RequestBody!, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetTokenAsync_ReadsTheTokenResponse()
        {
            (IOAuthTokenApi api, _) = CreateApi(
                responseJson: """{"access_token":"abc.def","token_type":"Bearer","expires_in":300,"scope":"read"}""");

            TokenResponse response = await api.GetTokenAsync(new TokenRequest());

            Assert.Equal("abc.def", response.AccessToken);
            Assert.Equal("Bearer", response.TokenType);
            Assert.Equal(300, response.ExpiresIn);
            Assert.Equal("read", response.Scope);
        }

        [Fact]
        public async Task GetTokenAsync_ThrowsOnRejection_RatherThanReturningNull()
        {
            // The whole point of the contrast with IServiceRequestApi: here a non-success
            // status has no useful meaning other than failure.
            (IOAuthTokenApi api, _) = CreateApi(
                HttpStatusCode.Unauthorized,
                """{"error":"invalid_client"}""");

            ApiException exception = await Assert.ThrowsAsync<ApiException>(
                () => api.GetTokenAsync(new TokenRequest()));

            Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        }
    }
}
