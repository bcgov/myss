namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Providers;

    /// <summary>
    /// Tests for <see cref="StrapiErrorMessageProvider"/>.
    /// </summary>
    public class StrapiErrorMessageProviderTests
    {
        // Wording deliberately distinct from the compiled defaults, so a passing
        // overlay test proves the provider read Strapi rather than falling back.
        private const string CatalogueBody = """
            {
              "data": [
                { "id": 1, "documentId": "msg-1", "keyword": "IDA.SIN.INVALID_CHECKSUM", "message": "Authored SIN wording" },
                { "id": 2, "documentId": "msg-2", "keyword": "NEW.KEYWORD.ADDED_IN_STRAPI", "message": "Wording for a keyword the code does not know yet" }
              ],
              "meta": { "pagination": { "page": 1, "pageSize": 100, "pageCount": 1, "total": 2 } }
            }
            """;

        private readonly StubHttpHandler _http = new();

        [Fact]
        public async Task GetMessages_OverlaysPublishedRowsOnTheDefaults()
        {
            _http.Bodies.Enqueue(CatalogueBody);
            StrapiErrorMessageProvider provider = NewProvider();

            IReadOnlyDictionary<string, string> catalogue = await provider.GetMessagesAsync(CancellationToken.None);

            // The published row wins over the compiled default.
            Assert.Equal("Authored SIN wording", catalogue[ValidationKeywords.SinInvalidChecksum]);
            // A keyword without a row keeps its default, so nothing goes unworded.
            Assert.Equal(
                ErrorMessageDefaults.Messages[ValidationKeywords.SinWrongLength],
                catalogue[ValidationKeywords.SinWrongLength]);
            // A row for a keyword the code does not know yet is served too: the
            // browser may word a new outcome before the API learns of it.
            Assert.Equal("Wording for a keyword the code does not know yet", catalogue["NEW.KEYWORD.ADDED_IN_STRAPI"]);
            Assert.Equal(ErrorMessageDefaults.Messages.Count + 1, catalogue.Count);
        }

        [Fact]
        public async Task GetMessages_QueriesPublishedRowsPageByPage()
        {
            // Strapi caps a page at rest.maxLimit, so a catalogue larger than
            // one page is only complete if every page is read.
            _http.Bodies.Enqueue("""
                {
                  "data": [ { "keyword": "PAGE.ONE.ROW", "message": "first page" } ],
                  "meta": { "pagination": { "page": 1, "pageSize": 100, "pageCount": 2, "total": 2 } }
                }
                """);
            _http.Bodies.Enqueue("""
                {
                  "data": [ { "keyword": "PAGE.TWO.ROW", "message": "second page" } ],
                  "meta": { "pagination": { "page": 2, "pageSize": 100, "pageCount": 2, "total": 2 } }
                }
                """);
            StrapiErrorMessageProvider provider = NewProvider();

            IReadOnlyDictionary<string, string> catalogue = await provider.GetMessagesAsync(CancellationToken.None);

            Assert.Equal(2, _http.Calls);
            Assert.Equal("first page", catalogue["PAGE.ONE.ROW"]);
            Assert.Equal("second page", catalogue["PAGE.TWO.ROW"]);
            Assert.All(_http.Requests, r => Assert.Equal("/api/error-messages", r.RequestUri!.AbsolutePath));
            Assert.Contains("pagination[page]=1", _http.Requests[0].RequestUri!.Query);
            Assert.Contains("pagination[page]=2", _http.Requests[1].RequestUri!.Query);
            Assert.Contains("pagination[pageSize]=100", _http.Requests[0].RequestUri!.Query);
        }

        [Fact]
        public async Task GetMessages_SendsTheConfiguredApiTokenAsABearerHeader()
        {
            _http.Bodies.Enqueue(CatalogueBody);
            StrapiErrorMessageProvider provider = NewProvider("tok-msg-123");

            await provider.GetMessagesAsync(CancellationToken.None);

            AuthenticationHeaderValue? auth = _http.LastRequest!.Headers.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Equal("tok-msg-123", auth.Parameter);
        }

        [Fact]
        public async Task GetMessages_UpstreamError_FallsBackToTheCompiledDefaults()
        {
            // Strapi down, or the token not yet granted error-message.find, must
            // NOT throw: a refused submission is still worded, from the defaults.
            _http.Status = HttpStatusCode.Forbidden;
            StrapiErrorMessageProvider provider = NewProvider();

            IReadOnlyDictionary<string, string> catalogue = await provider.GetMessagesAsync(CancellationToken.None);

            Assert.Equal(ErrorMessageDefaults.Messages, catalogue);
        }

        [Fact]
        public async Task GetMessages_MalformedBody_FallsBackToTheCompiledDefaults()
        {
            _http.Bodies.Enqueue("""{ "unexpected": true }""");
            StrapiErrorMessageProvider provider = NewProvider();

            IReadOnlyDictionary<string, string> catalogue = await provider.GetMessagesAsync(CancellationToken.None);

            Assert.Equal(ErrorMessageDefaults.Messages, catalogue);
        }

        [Fact]
        public async Task GetMessages_SkipsARowWithoutAKeywordAndAMessage()
        {
            // A blank or missing message must never blank a citizen's message:
            // the row is skipped and its keyword keeps the compiled wording.
            _http.Bodies.Enqueue("""
                {
                  "data": [
                    { "keyword": "IDA.SIN.WRONG_LENGTH", "message": "   " },
                    { "keyword": "IDA.EMAIL.MISMATCH" },
                    { "message": "no keyword" },
                    { "keyword": "IDA.EMAIL.INVALID_FORMAT", "message": " Authored email wording " }
                  ],
                  "meta": { "pagination": { "pageCount": 1 } }
                }
                """);
            StrapiErrorMessageProvider provider = NewProvider();

            IReadOnlyDictionary<string, string> catalogue = await provider.GetMessagesAsync(CancellationToken.None);

            Assert.Equal(ErrorMessageDefaults.Messages[ValidationKeywords.SinWrongLength], catalogue[ValidationKeywords.SinWrongLength]);
            Assert.Equal(ErrorMessageDefaults.Messages[ValidationKeywords.EmailMismatch], catalogue[ValidationKeywords.EmailMismatch]);
            Assert.Equal("Authored email wording", catalogue[ValidationKeywords.EmailInvalidFormat]);
            Assert.Equal(ErrorMessageDefaults.Messages.Count, catalogue.Count);
        }

        [Fact]
        public async Task GetMessages_EmptyData_ServesTheDefaults()
        {
            // A fresh Strapi before its seed ran: nothing published yet.
            _http.Bodies.Enqueue("""{ "data": [], "meta": { "pagination": { "pageCount": 0 } } }""");
            StrapiErrorMessageProvider provider = NewProvider();

            IReadOnlyDictionary<string, string> catalogue = await provider.GetMessagesAsync(CancellationToken.None);

            Assert.Equal(1, _http.Calls);
            Assert.Equal(ErrorMessageDefaults.Messages, catalogue);
        }

        [Fact]
        public async Task GetMessages_CachesTheCatalogue_SecondCallDoesNotReHitStrapi()
        {
            _http.Bodies.Enqueue(CatalogueBody);
            StrapiErrorMessageProvider provider = NewProvider();

            await provider.GetMessagesAsync(CancellationToken.None);
            await provider.GetMessagesAsync(CancellationToken.None);

            Assert.Equal(1, _http.Calls);
        }

        [Fact]
        public async Task GetMessages_CachesTheFallback_SecondCallDoesNotReHitStrapi()
        {
            // During a Strapi outage the fallback is cached too, so a burst of
            // refused submissions does not retry Strapi every time.
            _http.Status = HttpStatusCode.InternalServerError;
            StrapiErrorMessageProvider provider = NewProvider();

            await provider.GetMessagesAsync(CancellationToken.None);
            await provider.GetMessagesAsync(CancellationToken.None);

            Assert.Equal(1, _http.Calls);
        }

        private StrapiErrorMessageProvider NewProvider(string? apiToken = null)
        {
            var settings = new Dictionary<string, string?> { ["Strapi:BaseUrl"] = "http://strapi.test" };
            if (apiToken is not null)
            {
                settings["Strapi:ApiToken"] = apiToken;
            }

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            return new StrapiErrorMessageProvider(
                NullLogger<StrapiErrorMessageProvider>.Instance,
                new HttpClient(_http),
                new MemoryCache(new MemoryCacheOptions()),
                config);
        }

        /// <summary>
        /// Answers each request with the next queued body (the last one again
        /// once the queue is empty), so a paged read can be scripted.
        /// </summary>
        private sealed class StubHttpHandler : HttpMessageHandler
        {
            private string _lastBody = "{}";

            public Queue<string> Bodies { get; } = new();

            public List<HttpRequestMessage> Requests { get; } = [];

            public HttpRequestMessage? LastRequest => Requests.Count > 0 ? Requests[^1] : null;

            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            public int Calls => Requests.Count;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.Requests.Add(request);
                if (this.Bodies.Count > 0)
                {
                    _lastBody = this.Bodies.Dequeue();
                }

                var response = new HttpResponseMessage(this.Status)
                {
                    Content = new StringContent(_lastBody, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }
        }
    }
}
