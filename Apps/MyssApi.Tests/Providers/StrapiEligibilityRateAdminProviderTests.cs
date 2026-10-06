namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="StrapiEligibilityRateAdminProvider"/>, asserting the method,
    /// URL and body of every request.
    /// </summary>
    public sealed class StrapiEligibilityRateAdminProviderTests : IDisposable
    {
        private const string Today = "2026-10-06";

        private const string NoTableToday = """{ "data": [] }""";

        private const string TableToday = """
            { "data": [ { "id": 5, "documentId": "doc-today", "effectiveDate": "2026-10-06" } ] }
            """;

        private readonly QueueHttpHandler _http = new();

        public void Dispose() => _http.Dispose();

        [Fact]
        public async Task NoTableDatedToday_CreatesAndPublishesOne()
        {
            _http.Enqueue(Ok(NoTableToday));
            _http.Enqueue(Ok(SavedEcho()));

            SavedEligibilityRates saved = await Save();

            Assert.Equal(2, _http.Requests.Count);
            HttpRequestMessage lookup = _http.Requests[0];
            Assert.Equal(HttpMethod.Get, lookup.Method);
            Assert.Equal("/api/eligibility-rates", lookup.RequestUri!.AbsolutePath);
            string query = Uri.UnescapeDataString(lookup.RequestUri.Query);
            Assert.Contains($"filters[effectiveDate][$eq]={Today}", query, StringComparison.Ordinal);
            Assert.Contains("status=draft", query, StringComparison.Ordinal);
            Assert.Contains("pagination[limit]=2", query, StringComparison.Ordinal);

            HttpRequestMessage write = _http.Requests[1];
            Assert.Equal(HttpMethod.Post, write.Method);
            Assert.Equal("/api/eligibility-rates?status=published", write.RequestUri!.PathAndQuery);
            JsonNode data = Body(1)["data"]!;
            Assert.Equal(Today, (string?)data["effectiveDate"]);
            Assert.Equal(7, data["incomeRows"]!.AsArray().Count);
            Assert.Equal(2590.5m, (decimal)data["incomeRows"]![1]!["i"]!);
            Assert.Equal(10000m, (decimal)data["assetLimits"]!["b"]!);

            Assert.True(saved.Created);
            Assert.Equal(Today, saved.Rates.EffectiveDate);
            Assert.Equal(1360m, saved.Rates.IncomeRows[0].E);
        }

        [Fact]
        public async Task ATableDatedToday_IsUpdatedAndPublished()
        {
            _http.Enqueue(Ok(TableToday));
            _http.Enqueue(Ok(SavedEcho()));

            SavedEligibilityRates saved = await Save();

            HttpRequestMessage write = _http.Requests[1];
            Assert.Equal(HttpMethod.Put, write.Method);
            Assert.Equal("/api/eligibility-rates/doc-today?status=published", write.RequestUri!.PathAndQuery);
            JsonObject data = Body(1)["data"]!.AsObject();
            Assert.Equal(["incomeRows", "assetLimits"], data.Select(property => property.Key));
            Assert.False(saved.Created);
        }

        [Fact]
        public async Task EveryRequest_CarriesTheAdminToken()
        {
            _http.Enqueue(Ok(NoTableToday));
            _http.Enqueue(Ok(SavedEcho()));

            await Save();

            Assert.All(_http.Requests, request =>
            {
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("admin-token", request.Headers.Authorization?.Parameter);
            });
        }

        [Fact]
        public async Task ATableCreatedSinceTheLookup_IsUpdatedInstead()
        {
            // Another save created today's table between this save's lookup and its create.
            _http.Enqueue(Ok(NoTableToday));
            _http.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = Json("""{ "error": { "name": "ApplicationError", "details": { "keywords": ["RATES.EFFECTIVE_DATE.DUPLICATE"] } } }"""),
            });
            _http.Enqueue(Ok(TableToday));
            _http.Enqueue(Ok(SavedEcho()));

            SavedEligibilityRates saved = await Save();

            Assert.Equal(
                [HttpMethod.Get, HttpMethod.Post, HttpMethod.Get, HttpMethod.Put],
                _http.Requests.Select(request => request.Method));
            Assert.Equal("/api/eligibility-rates/doc-today?status=published", _http.Requests[3].RequestUri!.PathAndQuery);
            Assert.False(saved.Created);
        }

        [Fact]
        public async Task ADuplicateDateWithNoTableFound_IsRethrown()
        {
            _http.Enqueue(Ok(NoTableToday));
            _http.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = Json("""{ "error": { "details": { "keywords": ["RATES.EFFECTIVE_DATE.DUPLICATE"] } } }"""),
            });
            _http.Enqueue(Ok(NoTableToday));

            StrapiWriteException ex = await Assert.ThrowsAsync<StrapiWriteException>(Save);

            Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.Equal(3, _http.Requests.Count);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task ARefusedWrite_ThrowsStrapiWriteException_WithStrapisBody(HttpStatusCode status)
        {
            _http.Enqueue(Ok(NoTableToday));
            _http.Enqueue(new HttpResponseMessage(status) { Content = Json("""{ "error": { "message": "no" } }""") });

            StrapiWriteException ex = await Assert.ThrowsAsync<StrapiWriteException>(Save);

            Assert.Equal(status, ex.StatusCode);
            Assert.Contains("\"no\"", ex.Body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ARefusedLookup_IsContentEngineUnavailable_AndNothingIsWritten()
        {
            _http.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = Json("{}") });

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(Save);

            Assert.Single(_http.Requests);
        }

        [Theory]
        [InlineData("""{ "data": {} }""")]
        [InlineData("not json")]
        public async Task AMalformedLookup_IsContentEngineUnavailable_AndNothingIsWritten(string body)
        {
            _http.Enqueue(Ok(body));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(Save);

            Assert.Single(_http.Requests);
        }

        [Fact]
        public async Task TwoTablesDatedToday_AreContentEngineUnavailable_AndNothingIsWritten()
        {
            _http.Enqueue(Ok("""
                { "data": [ { "documentId": "doc-1" }, { "documentId": "doc-2" } ] }
                """));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(Save);

            Assert.Single(_http.Requests);
        }

        [Fact]
        public async Task ALookupRowWithoutADocumentId_IsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("""{ "data": [ { "effectiveDate": "2026-10-06" } ] }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(Save);

            Assert.Single(_http.Requests);
        }

        [Fact]
        public async Task AConnectionFailure_IsContentEngineUnavailable()
        {
            _http.EnqueueThrow(new HttpRequestException("connection refused"));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(Save);
        }

        [Fact]
        public async Task ATimeout_IsContentEngineUnavailable()
        {
            _http.EnqueueThrow(new TaskCanceledException("timed out"));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(Save);
        }

        [Fact]
        public async Task TheCallersOwnCancellation_PropagatesAsACancellation()
        {
            using CancellationTokenSource cancelled = new();
            await cancelled.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Save(cancelled.Token));
        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("""{ "data": null }""")]
        [InlineData("""{ "data": { "effectiveDate": "2026-10-06" } }""")]
        public async Task AnEmptyOrIncompleteEcho_IsContentEngineUnavailable(string echo)
        {
            _http.Enqueue(Ok(NoTableToday));
            _http.Enqueue(Ok(echo));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(Save);
        }

        [Fact]
        public void AMissingBaseUrl_FailsAtConstruction()
        {
            IConfiguration config = new ConfigurationBuilder().Build();

            Assert.Throws<InvalidOperationException>(() => new StrapiEligibilityRateAdminProvider(
                NullLogger<StrapiEligibilityRateAdminProvider>.Instance, new HttpClient(_http), config));
        }

        private Task<SavedEligibilityRates> Save() => Save(CancellationToken.None);

        private Task<SavedEligibilityRates> Save(CancellationToken cancellationToken)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Strapi:BaseUrl"] = "http://strapi.test",
                    ["Strapi:AdminApiToken"] = "admin-token",
                })
                .Build();
            StrapiEligibilityRateAdminProvider provider = new(
                NullLogger<StrapiEligibilityRateAdminProvider>.Instance, new HttpClient(_http), config);
            return provider.SaveTableAsync(
                Today,
                EligibilityRatesTestData.SeededRows(),
                EligibilityRatesTestData.SeededAssetLimits(),
                cancellationToken);
        }

        private JsonNode Body(int index) => JsonNode.Parse(_http.Bodies[index]!)!;

        private static string SavedEcho()
        {
            JsonNode entity = JsonSerializer.SerializeToNode(
                EligibilityRatesTestData.SeededTable(Today), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            entity["documentId"] = "doc-today";
            entity["publishedAt"] = "2026-10-06T17:00:00.000Z";
            return new JsonObject { ["data"] = entity }.ToJsonString();
        }

        private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

        private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = Json(body) };

        private sealed class QueueHttpHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpResponseMessage>> _steps = new();

            public List<HttpRequestMessage> Requests { get; } = [];

            public List<string?> Bodies { get; } = [];

            public void Enqueue(HttpResponseMessage response) => _steps.Enqueue(() => response);

            public void EnqueueThrow(Exception ex) => _steps.Enqueue(() => throw ex);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // A real handler stops when the caller cancels.
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
                return _steps.Count > 0
                    ? _steps.Dequeue()()
                    : throw new InvalidOperationException("No response queued for this request.");
            }
        }
    }
}
