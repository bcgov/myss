namespace Myss.Common.Tests.Content
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Common.Content;

    /// <summary>
    /// Tests for <see cref="FormMessageReader"/>: the query it sends, the
    /// entries it maps, and the ones it leaves out. What each API does when the content
    /// engine cannot be read is tested with its own provider or service.
    /// </summary>
    public sealed class FormMessageReaderTests : IDisposable
    {
        private const string TwoEntries = """
            {
              "data": [
                {
                  "id": 2,
                  "documentId": "doc-newer",
                  "formSpecId": "bc-bus-pass",
                  "messageKey": "confirmation",
                  "heading": "Request complete",
                  "title": "Submitted",
                  "body": "Your reference number is {referenceNumber}.",
                  "changedBy": "JSMITH",
                  "publishedAt": "2026-07-14T17:42:00.000Z"
                },
                {
                  "id": 1,
                  "documentId": "doc-older",
                  "formSpecId": "bc-bus-pass",
                  "messageKey": "confirmation",
                  "heading": "Older heading",
                  "title": "Older title",
                  "body": "Older {referenceNumber}",
                  "changedBy": "KLEE",
                  "publishedAt": "2026-06-02T16:00:00.000Z"
                }
              ]
            }
            """;

        private readonly StubHttpHandler _http = new();

        private readonly CapturingLogger _logger = new();

        /// <inheritdoc/>
        public void Dispose() => _http.Dispose();

        [Fact]
        public async Task ReadNewest_QueriesTheMessagesNewestPublishedEntries()
        {
            _http.Body = TwoEntries;

            await NewReader().ReadNewestAsync("bc-bus-pass", "confirmation", 20, CancellationToken.None);

            Assert.Equal("/api/form-messages", _http.LastRequest!.RequestUri!.AbsolutePath);
            string query = _http.LastRequest!.RequestUri!.Query;
            Assert.Contains("filters[formSpecId][$eq]=bc-bus-pass&", query, StringComparison.Ordinal);
            Assert.Contains("filters[messageKey][$eq]=confirmation&", query, StringComparison.Ordinal);
            Assert.Contains("sort=publishedAt:desc", query, StringComparison.Ordinal);
            Assert.Contains("pagination[pageSize]=20", query, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReadNewest_EscapesTheFormIdAndKey()
        {
            _http.Body = """{ "data": [] }""";

            await NewReader().ReadNewestAsync("a b&sort=x", "c d&e=f", 1, CancellationToken.None);

            string query = _http.LastRequest!.RequestUri!.Query;
            Assert.Contains("filters[formSpecId][$eq]=a%20b%26sort%3Dx&", query, StringComparison.Ordinal);
            Assert.Contains("filters[messageKey][$eq]=c%20d%26e%3Df&", query, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReadNewest_MapsEachEntry_NewestFirst()
        {
            _http.Body = TwoEntries;

            IReadOnlyList<FormMessageEntry> entries =
                await NewReader().ReadNewestAsync("bc-bus-pass", "confirmation", 20, CancellationToken.None);

            Assert.Equal(["doc-newer", "doc-older"], entries.Select(entry => entry.Id));
            FormMessageEntry newest = entries[0];
            Assert.Equal("Request complete", newest.Heading);
            Assert.Equal("Submitted", newest.Title);
            Assert.Equal("Your reference number is {referenceNumber}.", newest.Body);
            Assert.Equal("JSMITH", newest.ChangedBy);
            Assert.Equal(new DateTimeOffset(2026, 7, 14, 17, 42, 0, TimeSpan.Zero), newest.ChangedAt);
        }

        [Fact]
        public async Task ReadNewest_LeavesOutAnEntryWithAMissingOrBlankField_AndLogsHowMany()
        {
            _http.Body = """
                {
                  "data": [
                    { "documentId": "blank", "heading": "  ", "title": "t", "body": "b", "changedBy": "c", "publishedAt": "2026-07-14T17:42:00Z" },
                    { "documentId": "no-author", "heading": "h", "title": "t", "body": "b", "publishedAt": "2026-07-14T17:41:00Z" },
                    { "documentId": "no-date", "heading": "h", "title": "t", "body": "b", "changedBy": "c" },
                    { "documentId": "good", "heading": "h", "title": "t", "body": "b", "changedBy": "c", "publishedAt": "2026-07-14T17:40:00Z" }
                  ]
                }
                """;

            IReadOnlyList<FormMessageEntry> entries =
                await NewReader().ReadNewestAsync("bc-bus-pass", "confirmation", 20, CancellationToken.None);

            Assert.Equal("good", Assert.Single(entries).Id);
            Assert.Contains(_logger.Messages, message => message.Contains("Left out 3", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ReadNewest_NonePublished_IsAnEmptyList()
        {
            _http.Body = """{ "data": [], "meta": { "pagination": { "total": 0 } } }""";

            IReadOnlyList<FormMessageEntry> entries =
                await NewReader().ReadNewestAsync("bc-bus-pass", "confirmation", 1, CancellationToken.None);

            Assert.Empty(entries);
            Assert.Empty(_logger.Messages);
        }

        [Fact]
        public async Task ReadNewest_SendsTheConfiguredApiTokenAsABearerHeader()
        {
            _http.Body = TwoEntries;

            await NewReader("tok-ack-123").ReadNewestAsync("bc-bus-pass", "confirmation", 1, CancellationToken.None);

            AuthenticationHeaderValue? auth = _http.LastRequest!.Headers.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Equal("tok-ack-123", auth.Parameter);
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task ReadNewest_UpstreamError_Throws(HttpStatusCode status)
        {
            _http.Status = status;

            await Assert.ThrowsAsync<HttpRequestException>(
                () => NewReader().ReadNewestAsync("bc-bus-pass", "confirmation", 1, CancellationToken.None));
        }

        [Theory]
        [InlineData("<html>maintenance</html>")]
        [InlineData("""{ "error": "no data" }""")]
        [InlineData("""{ "data": {} }""")]
        [InlineData("[]")]
        public async Task ReadNewest_ReplyWithoutADataArray_Throws(string body)
        {
            _http.Body = body;

            await Assert.ThrowsAnyAsync<JsonException>(
                () => NewReader().ReadNewestAsync("bc-bus-pass", "confirmation", 1, CancellationToken.None));
        }

        private FormMessageReader NewReader(string? apiToken = null)
        {
            var settings = new Dictionary<string, string?> { ["Strapi:BaseUrl"] = "http://strapi.test" };
            if (apiToken is not null)
            {
                settings["Strapi:ApiToken"] = apiToken;
            }

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            return new FormMessageReader(
                _logger,
                new HttpClient(_http),
                StrapiReadOptions.FromConfiguration(config, "Strapi:ApiToken"));
        }

        private sealed class CapturingLogger : ILogger<FormMessageReader>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => this.Messages.Add(formatter(state, exception));
        }

        private sealed class StubHttpHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            public string Body { get; set; } = "{}";

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.LastRequest = request;
                var response = new HttpResponseMessage(this.Status)
                {
                    Content = new StringContent(this.Body, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }
        }
    }
}
