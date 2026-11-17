namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Text;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Common.Content;

    /// <summary>
    /// Tests for <see cref="StrapiFormMessageProvider"/>: what it returns
    /// for each answer of the content engine, and how long it keeps each result.
    /// </summary>
    public sealed class StrapiFormMessageProviderTests : IDisposable
    {
        private const string FormSpecId = "bc-bus-pass";

        private const string MessageKey = "confirmation";

        private const string OneEntry = """
            {
              "data": [
                {
                  "documentId": "doc-1",
                  "heading": "Request complete",
                  "title": "Submitted",
                  "body": "Your reference number is {referenceNumber}.",
                  "changedBy": "JSMITH",
                  "publishedAt": "2026-07-14T17:42:00.000Z"
                }
              ]
            }
            """;

        private const string NoEntries = """{ "data": [] }""";

        private readonly StubHttpHandler _http = new();
        private readonly RecordingMemoryCache _cache = new();

        /// <inheritdoc/>
        public void Dispose()
        {
            _http.Dispose();
            _cache.Dispose();
        }

        [Fact]
        public async Task GetLive_ReturnsTheNewestEntrysTexts()
        {
            _http.Body = OneEntry;

            FormMessageLookup lookup = await NewProvider().GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);

            Assert.Equal(FormMessageStatus.Found, lookup.Status);
            Assert.NotNull(lookup.Message);
            Assert.Equal("Request complete", lookup.Message.Heading);
            Assert.Equal("Submitted", lookup.Message.Title);
            Assert.Equal("Your reference number is {referenceNumber}.", lookup.Message.Body);
        }

        [Fact]
        public async Task GetLive_SkipsANewestEntryWithABlankField_ForTheNextOne()
        {
            _http.Body = """
                {
                  "data": [
                    { "documentId": "blank", "heading": " ", "title": "t", "body": "b", "changedBy": "c", "publishedAt": "2026-07-14T17:43:00Z" },
                    { "documentId": "doc-1", "heading": "Previous heading", "title": "t", "body": "b", "changedBy": "c", "publishedAt": "2026-07-14T17:42:00Z" }
                  ]
                }
                """;

            FormMessageLookup lookup = await NewProvider().GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);

            Assert.Equal("Previous heading", lookup.Message?.Heading);
            Assert.Contains("pagination[pageSize]=5", _http.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetLive_AContentEngineThatDoesNotAnswerInTime_IsUnavailableFor30Seconds()
        {
            _http.Failure = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");

            FormMessageLookup lookup = await NewProvider().GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);

            Assert.Equal(FormMessageStatus.Unavailable, lookup.Status);
            Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(_cache.Created).AbsoluteExpirationRelativeToNow);
        }

        [Fact]
        public async Task GetLive_NothingPublished_IsNone()
        {
            _http.Body = NoEntries;

            FormMessageLookup lookup = await NewProvider().GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);

            Assert.Equal(FormMessageStatus.None, lookup.Status);
            Assert.Null(lookup.Message);
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError, OneEntry)]
        [InlineData(HttpStatusCode.Forbidden, OneEntry)]
        [InlineData(HttpStatusCode.OK, "<html>maintenance</html>")]
        public async Task GetLive_ContentEngineCannotBeRead_IsUnavailable(HttpStatusCode status, string body)
        {
            _http.Status = status;
            _http.Body = body;

            FormMessageLookup lookup = await NewProvider().GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);

            Assert.Equal(FormMessageStatus.Unavailable, lookup.Status);
            Assert.Null(lookup.Message);
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, OneEntry)]
        [InlineData(HttpStatusCode.OK, NoEntries)]
        [InlineData(HttpStatusCode.InternalServerError, OneEntry)]
        public async Task GetLive_SecondCall_DoesNotReadTheContentEngineAgain(HttpStatusCode status, string body)
        {
            _http.Status = status;
            _http.Body = body;
            StrapiFormMessageProvider provider = NewProvider();

            FormMessageLookup first = await provider.GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);
            FormMessageLookup second = await provider.GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);

            Assert.Equal(1, _http.Calls);
            Assert.Same(first, second);
        }

        [Fact]
        public async Task GetLive_CachesEachFormSeparately()
        {
            _http.Body = NoEntries;
            StrapiFormMessageProvider provider = NewProvider();

            await provider.GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);
            await provider.GetLiveAsync("another-form", MessageKey, CancellationToken.None);

            Assert.Equal(2, _http.Calls);
        }

        [Fact]
        public async Task GetLive_CachesEachMessageOfAFormSeparately()
        {
            _http.Body = NoEntries;
            StrapiFormMessageProvider provider = NewProvider();

            await provider.GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);
            await provider.GetLiveAsync(FormSpecId, "intro", CancellationToken.None);

            Assert.Equal(2, _http.Calls);
            Assert.Contains("filters[messageKey][$eq]=intro&", _http.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, OneEntry, 60)]
        [InlineData(HttpStatusCode.OK, NoEntries, 60)]
        [InlineData(HttpStatusCode.InternalServerError, OneEntry, 30)]
        public async Task GetLive_KeepsEachResultForItsWindow(HttpStatusCode status, string body, int seconds)
        {
            _http.Status = status;
            _http.Body = body;

            await NewProvider().GetLiveAsync(FormSpecId, MessageKey, CancellationToken.None);

            ICacheEntry entry = Assert.Single(_cache.Created);
            Assert.Equal(TimeSpan.FromSeconds(seconds), entry.AbsoluteExpirationRelativeToNow);
        }

        [Fact]
        public async Task GetLive_CallerCancels_RethrowsAndCachesNothing()
        {
            _http.Body = OneEntry;
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => NewProvider().GetLiveAsync(FormSpecId, MessageKey, cancellation.Token));

            Assert.Empty(_cache.Created);
        }

        private StrapiFormMessageProvider NewProvider()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Strapi:BaseUrl"] = "http://strapi.test" })
                .Build();

            var reader = new FormMessageReader(
                NullLogger<FormMessageReader>.Instance,
                new HttpClient(_http),
                StrapiReadOptions.FromConfiguration(config, "Strapi:ApiToken"));

            return new StrapiFormMessageProvider(
                NullLogger<StrapiFormMessageProvider>.Instance,
                reader,
                _cache);
        }

        /// <summary>A real memory cache that also keeps every entry it creates, so a test can read its expiry.</summary>
        private sealed class RecordingMemoryCache : IMemoryCache
        {
            private readonly MemoryCache _inner = new(new MemoryCacheOptions());

            public List<ICacheEntry> Created { get; } = [];

            public ICacheEntry CreateEntry(object key)
            {
                ICacheEntry entry = _inner.CreateEntry(key);
                this.Created.Add(entry);
                return entry;
            }

            public void Remove(object key) => _inner.Remove(key);

            public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);

            public void Dispose() => _inner.Dispose();
        }

        private sealed class StubHttpHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            public string Body { get; set; } = "{}";

            public int Calls { get; private set; }

            public Exception? Failure { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.Calls++;
                this.LastRequest = request;
                if (this.Failure is not null)
                {
                    throw this.Failure;
                }

                var response = new HttpResponseMessage(this.Status)
                {
                    Content = new StringContent(this.Body, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }
        }
    }
}
