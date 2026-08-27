namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Providers;

    /// <summary>
    /// Tests for <see cref="CdogsPdfProvider"/>.
    /// </summary>
    public class CdogsPdfProviderTests
    {
        [Fact]
        public async Task GenerateFromOdtAsync_RequestsTokenThenRendersPdf()
        {
            /// dummy data to pass to the provider
            byte[] expectedPdf = [1, 2, 3, 4, 5];
            var handler = new SequencedHttpHandler(
            [
                /// dummy token to pass to the provider
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "access_token": "token-123" }""", Encoding.UTF8, "application/json"),
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(expectedPdf),
                },
            ]);

            CdogsPdfProvider provider = NewProvider(handler);

            byte[] template = [0x50, 0x4b, 0x03, 0x04];
            byte[] pdf = await provider.GenerateFromOdtAsync(
                template,
                new { firstName = "Jane" },
                CancellationToken.None);

            Assert.Equal(expectedPdf, pdf);
            Assert.Equal(2, handler.Requests.Count);

            CapturedRequest tokenRequest = handler.Requests[0];
            Assert.Equal(HttpMethod.Post, tokenRequest.Method);
            Assert.Equal("https://token.test/oauth/token", tokenRequest.RequestUri);

            string tokenBody = tokenRequest.Body;
            Assert.Contains("grant_type=client_credentials", tokenBody);
            Assert.Contains("client_id=client-id", tokenBody);
            Assert.Contains("client_secret=client-secret", tokenBody);

            CapturedRequest renderRequest = handler.Requests[1];
            Assert.Equal(HttpMethod.Post, renderRequest.Method);
            Assert.Equal("https://cdogs.test/api/v2/template/render", renderRequest.RequestUri);
            Assert.Equal("Bearer", renderRequest.AuthorizationScheme);
            Assert.Equal("token-123", renderRequest.AuthorizationParameter);

            string renderBody = renderRequest.Body;
            using JsonDocument body = JsonDocument.Parse(renderBody);
            Assert.Equal(JsonValueKind.Object, body.RootElement.GetProperty("data").ValueKind);
            Assert.Equal("odt", body.RootElement.GetProperty("template").GetProperty("fileType").GetString());
            Assert.Equal("base64", body.RootElement.GetProperty("template").GetProperty("encodingType").GetString());
            Assert.Equal("pdf", body.RootElement.GetProperty("options").GetProperty("convertTo").GetString());
            Assert.True(body.RootElement.GetProperty("options").GetProperty("overwrite").GetBoolean());
            Assert.Equal("Jane", body.RootElement.GetProperty("data").GetProperty("firstName").GetString());

            string encodedTemplate = body.RootElement.GetProperty("template").GetProperty("content").GetString()!;
            Assert.Equal(template, Convert.FromBase64String(encodedTemplate));
        }

        [Fact]
        public async Task GenerateFromOdtAsync_ThrowsWhenRequiredConfigIsMissing()
        {
            var settings = new Dictionary<string, string?>
            {
                ["Cdogs:BaseUrl"] = "https://cdogs.test/api/v2",
            };
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var provider = new CdogsPdfProvider(
                NullLogger<CdogsPdfProvider>.Instance,
                new HttpClient(new SequencedHttpHandler([])),
                config);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GenerateFromOdtAsync([1], new { }, CancellationToken.None));
        }

        private static CdogsPdfProvider NewProvider(HttpMessageHandler handler)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Cdogs:BaseUrl"] = "https://cdogs.test/api/v2",
                ["Cdogs:TokenEndpoint"] = "https://token.test/oauth/token",
                ["Cdogs:ClientId"] = "client-id",
                ["Cdogs:ClientSecret"] = "client-secret",
            };
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new CdogsPdfProvider(
                NullLogger<CdogsPdfProvider>.Instance,
                new HttpClient(handler),
                config);
        }

    
        /// fake http handler which lets tests intercept request/response
        private sealed class SequencedHttpHandler : HttpMessageHandler
        {
            private readonly Queue<HttpResponseMessage> _responses;

            public SequencedHttpHandler(IEnumerable<HttpResponseMessage> responses)
            {
                _responses = new Queue<HttpResponseMessage>(responses);
            }

            public List<CapturedRequest> Requests { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                Requests.Add(new CapturedRequest(
                    request.Method,
                    request.RequestUri?.ToString() ?? string.Empty,
                    request.Headers.Authorization?.Scheme,
                    request.Headers.Authorization?.Parameter,
                    body));

                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException("No queued response for HTTP request.");
                }

                return _responses.Dequeue();
            }
        }

        private sealed record CapturedRequest(
            HttpMethod Method,
            string RequestUri,
            string? AuthorizationScheme,
            string? AuthorizationParameter,
            string Body);
    }
}