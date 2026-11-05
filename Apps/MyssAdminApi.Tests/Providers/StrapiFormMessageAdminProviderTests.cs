namespace Myss.AdminApi.Tests.Providers
{
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.AdminApi.Providers;
    using Myss.Common.Content;

    /// <summary>
    /// Tests for <see cref="StrapiFormMessageAdminProvider"/>: the write it
    /// sends, what it makes of each reply, and how a failed read is reported.
    /// </summary>
    public sealed class StrapiFormMessageAdminProviderTests : IDisposable
    {
        private const string CreatedReply = """
            {
              "data": {
                "id": 7,
                "documentId": "doc-new",
                "formSpecId": "bc-bus-pass",
                "messageKey": "confirmation",
                "heading": "Heading",
                "title": "Title",
                "body": "Body {referenceNumber}",
                "changedBy": "MADMIN",
                "publishedAt": "2026-07-14T17:42:00.000Z"
              },
              "meta": {}
            }
            """;

        private static readonly FormMessageTexts Texts = new("Heading", "Title", "Body {referenceNumber}");

        private readonly StubHttpHandler _http = new();

        /// <inheritdoc/>
        public void Dispose() => _http.Dispose();

        [Fact]
        public async Task Create_PostsThePublishedEntryInADataEnvelope_WithTheWriteToken()
        {
            _http.Body = CreatedReply;

            await NewProvider().CreateAsync("bc-bus-pass", "confirmation", Texts, "MADMIN", CancellationToken.None);

            HttpRequestMessage request = _http.Requests.Single();
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/form-messages", request.RequestUri!.AbsolutePath);
            Assert.Equal("?status=published", request.RequestUri.Query);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("write-token", request.Headers.Authorization?.Parameter);

            using JsonDocument body = JsonDocument.Parse(_http.RequestBodies.Single());
            JsonElement data = Assert.Single(body.RootElement.EnumerateObject(), property => property.Name == "data").Value;
            Assert.Equal(
                [
                    ("formSpecId", "bc-bus-pass"),
                    ("messageKey", "confirmation"),
                    ("heading", "Heading"),
                    ("title", "Title"),
                    ("body", "Body {referenceNumber}"),
                    ("changedBy", "MADMIN"),
                ],
                data.EnumerateObject().Select(property => (property.Name, property.Value.GetString())));
        }

        [Fact]
        public async Task Create_ReturnsTheStoredEntry()
        {
            _http.Body = CreatedReply;

            FormMessageEntry entry =
                await NewProvider().CreateAsync("bc-bus-pass", "confirmation", Texts, "MADMIN", CancellationToken.None);

            Assert.Equal("doc-new", entry.Id);
            Assert.Equal("MADMIN", entry.ChangedBy);
            Assert.Equal(new DateTimeOffset(2026, 7, 14, 17, 42, 0, TimeSpan.Zero), entry.ChangedAt);
        }

        [Fact]
        public async Task Create_ARefusal_ThrowsStrapiWriteException_WithTheBody()
        {
            const string refusal = """{ "data": null, "error": { "status": 400, "name": "ValidationError" } }""";
            _http.Status = HttpStatusCode.BadRequest;
            _http.Body = refusal;

            StrapiWriteException ex = await Assert.ThrowsAsync<StrapiWriteException>(
                () => NewProvider().CreateAsync("bc-bus-pass", "confirmation", Texts, "MADMIN", CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.Equal(refusal, ex.Body);
        }

        [Theory]
        [InlineData("<html>maintenance</html>")]
        [InlineData("""{ "data": null }""")]
        [InlineData("""{ "data": { "documentId": "doc-new" } }""")]
        public async Task Create_AnUnreadableReply_ThrowsContentEngineUnavailable(string reply)
        {
            _http.Body = reply;

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewProvider().CreateAsync("bc-bus-pass", "confirmation", Texts, "MADMIN", CancellationToken.None));
        }

        [Fact]
        public async Task Create_AnUnreachableContentEngine_ThrowsContentEngineUnavailable()
        {
            _http.Failure = new HttpRequestException("Connection refused");

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewProvider().CreateAsync("bc-bus-pass", "confirmation", Texts, "MADMIN", CancellationToken.None));
        }

        [Fact]
        public async Task ReadAndCreate_AContentEngineThatDoesNotAnswerInTime_ThrowContentEngineUnavailable()
        {
            _http.Failure = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
            StrapiFormMessageAdminProvider provider = NewProvider();

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => provider.ReadNewestAsync("bc-bus-pass", "confirmation", 20, CancellationToken.None));
            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => provider.CreateAsync("bc-bus-pass", "confirmation", Texts, "MADMIN", CancellationToken.None));
        }

        [Fact]
        public async Task ReadNewest_ReadsThroughTheSharedReader()
        {
            _http.Body = """{ "data": [ """ + CreatedReplyEntry() + """ ] }""";

            IReadOnlyList<FormMessageEntry> entries =
                await NewProvider().ReadNewestAsync("bc-bus-pass", "confirmation", 20, CancellationToken.None);

            Assert.Equal("doc-new", Assert.Single(entries).Id);
            string query = _http.Requests.Single().RequestUri!.Query;
            Assert.Contains("filters[formSpecId][$eq]=bc-bus-pass&", query, StringComparison.Ordinal);
            Assert.Contains("filters[messageKey][$eq]=confirmation&", query, StringComparison.Ordinal);
            Assert.Contains("pagination[pageSize]=20", query, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError, """{ "data": [] }""")]
        [InlineData(HttpStatusCode.Forbidden, """{ "data": [] }""")]
        [InlineData(HttpStatusCode.OK, "<html>maintenance</html>")]
        public async Task ReadNewest_AnUnreadableContentEngine_ThrowsContentEngineUnavailable(HttpStatusCode status, string body)
        {
            _http.Status = status;
            _http.Body = body;

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewProvider().ReadNewestAsync("bc-bus-pass", "confirmation", 20, CancellationToken.None));
        }

        private static string CreatedReplyEntry()
        {
            using JsonDocument reply = JsonDocument.Parse(CreatedReply);
            return reply.RootElement.GetProperty("data").GetRawText();
        }

        private StrapiFormMessageAdminProvider NewProvider()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Strapi:BaseUrl"] = "http://strapi.test",
                    ["Strapi:AdminApiToken"] = "write-token",
                })
                .Build();

            var reader = new FormMessageReader(
                NullLogger<FormMessageReader>.Instance,
                new HttpClient(_http, disposeHandler: false),
                StrapiReadOptions.FromConfiguration(config, "Strapi:AdminApiToken"));

            return new StrapiFormMessageAdminProvider(
                NullLogger<StrapiFormMessageAdminProvider>.Instance,
                new HttpClient(_http, disposeHandler: false),
                config,
                reader);
        }

        private sealed class StubHttpHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];

            public List<string> RequestBodies { get; } = [];

            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            public string Body { get; set; } = "{}";

            public Exception? Failure { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.Requests.Add(request);
                if (request.Content is not null)
                {
                    this.RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
                }

                if (this.Failure is not null)
                {
                    throw this.Failure;
                }

                return new HttpResponseMessage(this.Status)
                {
                    Content = new StringContent(this.Body, Encoding.UTF8, "application/json"),
                };
            }
        }
    }
}
