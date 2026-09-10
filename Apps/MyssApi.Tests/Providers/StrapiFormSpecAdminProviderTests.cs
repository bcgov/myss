namespace Myss.Api.Tests.Providers
{
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Tests for <see cref="StrapiFormSpecAdminProvider"/>. The Strapi calls are
    /// mocked with a queued <see cref="HttpMessageHandler"/>; each test enqueues
    /// the responses in the exact order the method under test issues its requests.
    /// </summary>
    public class StrapiFormSpecAdminProviderTests
    {
        private readonly QueueHttpHandler _http = new();

        [Fact]
        public async Task ListForms_GroupsByFormSpecId_AndFlagsPublishedVersions()
        {
            // First GET = published copies; second GET = draft copies.
            _http.Enqueue(Ok(Data(
                Row("A", 1, "Form A"),
                Row("A", 2, "Form A"),
                Row("B", 1, "Form B"))));
            _http.Enqueue(Ok(Data(
                Row("A", 1, "Form A"),
                Row("A", 2, "Form A"),
                Row("A", 3, "Form A"),   // draft-only, never published
                Row("B", 1, "Form B"))));

            IReadOnlyList<FormSummaryModel> forms = await NewProvider().ListFormsAsync(CancellationToken.None);

            Assert.Equal(2, forms.Count);
            FormSummaryModel a = forms.Single(f => f.FormSpecId == "A");
            Assert.Equal("Form A", a.Title);
            Assert.Equal(new[] { 1, 2, 3 }, a.Versions.Select(v => v.Version));
            Assert.True(a.Versions.Single(v => v.Version == 2).IsPublished);
            Assert.False(a.Versions.Single(v => v.Version == 3).IsPublished);

            // Two reads, both scoped by status, spec omitted for cheapness.
            Assert.Equal(2, _http.Requests.Count);
            Assert.Contains("status=published", _http.Requests[0].RequestUri!.Query);
            Assert.Contains("status=draft", _http.Requests[1].RequestUri!.Query);
            Assert.Contains("fields", _http.Requests[0].RequestUri!.Query);
        }

        [Fact]
        public async Task GetDraft_ReturnsTheLatestDraftSpec()
        {
            _http.Enqueue(Ok(Data(RowWithSpec("estimator", 4, "Estimator", """{ "display": "form" }"""))));

            FormSpecModel? spec = await NewProvider().GetDraftAsync("estimator", CancellationToken.None);

            Assert.NotNull(spec);
            Assert.Equal("estimator", spec.FormSpecId);
            Assert.Equal(4, spec.Version);
            Assert.Equal("form", spec.Spec.GetProperty("display").GetString());
            Assert.Contains("status=draft", _http.Requests[0].RequestUri!.Query);
        }

        [Fact]
        public async Task GetDraft_UnknownForm_ReturnsNull()
        {
            _http.Enqueue(Ok("""{ "data": [] }"""));

            FormSpecModel? spec = await NewProvider().GetDraftAsync("nope", CancellationToken.None);

            Assert.Null(spec);
        }

        [Fact]
        public async Task SaveDraft_NewForm_CreatesDraftAtVersionOne()
        {
            _http.Enqueue(Ok("""{ "data": [] }"""));                 // draft latest -> none
            _http.Enqueue(Ok("""{ "data": [] }"""));                 // published latest -> none
            _http.Enqueue(Ok(SingleData(RowWithSpec("newform", 1, "New", "{}"))));  // POST create

            JsonElement spec = Spec("""{ "display": "form", "components": [] }""");
            FormSpecModel saved = await NewProvider().SaveDraftAsync("newform", spec, "New", CancellationToken.None);

            Assert.Equal(1, saved.Version);
            HttpRequestMessage post = _http.Requests[^1];
            Assert.Equal(HttpMethod.Post, post.Method);
            Assert.Contains("/api/form-specs", post.RequestUri!.AbsolutePath);
            Assert.Contains("status=draft", post.RequestUri!.Query);
            JsonElement data = JsonDocument.Parse(_http.Bodies[^1]!).RootElement.GetProperty("data");
            Assert.Equal("newform", data.GetProperty("formSpecId").GetString());
            Assert.Equal(1, data.GetProperty("version").GetInt32());
        }

        [Fact]
        public async Task SaveDraft_PublishedFormNoDraft_CreatesNextVersion()
        {
            // Latest draft is only the draft-copy of published v3 (not in-progress),
            // so the edit must become a new version 4.
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));   // draft latest -> v3
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));   // published latest -> v3 (3 !> 3)
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));   // published latest (NextVersion) -> v3
            _http.Enqueue(Ok(SingleData(RowWithSpec("f", 4, "F", "{}"))));  // POST create v4

            FormSpecModel saved = await NewProvider().SaveDraftAsync("f", Spec("{}"), "F", CancellationToken.None);

            Assert.Equal(4, saved.Version);
            JsonElement data = JsonDocument.Parse(_http.Bodies[^1]!).RootElement.GetProperty("data");
            Assert.Equal(4, data.GetProperty("version").GetInt32());
            Assert.Equal(HttpMethod.Post, _http.Requests[^1].Method);
        }

        [Fact]
        public async Task SaveDraft_InProgressDraft_UpdatesInPlace()
        {
            _http.Enqueue(Ok(Data(RowWithDoc("f", 4, "d4"))));   // draft latest -> v4 (doc d4)
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));           // published latest -> v3 (4 > 3)
            _http.Enqueue(Ok(SingleData(RowWithSpec("f", 4, "F", "{}"))));  // PUT update

            FormSpecModel saved = await NewProvider().SaveDraftAsync("f", Spec("{}"), "F", CancellationToken.None);

            Assert.Equal(4, saved.Version);
            HttpRequestMessage put = _http.Requests[^1];
            Assert.Equal(HttpMethod.Put, put.Method);
            Assert.Contains("/api/form-specs/d4", put.RequestUri!.AbsolutePath);
            Assert.Contains("status=draft", put.RequestUri!.Query);
        }

        [Fact]
        public async Task Publish_FlipsTheInProgressDraft_AndReturnsItsVersion()
        {
            _http.Enqueue(Ok(Data(RowWithDoc("f", 4, "d4"))));   // draft latest -> v4 (doc d4)
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));           // published latest -> v3
            _http.Enqueue(Ok(SingleData(RowWithSpec("f", 4, "F", "{}"))));  // PUT publish

            int version = await NewProvider().PublishAsync("f", CancellationToken.None);

            Assert.Equal(4, version);
            HttpRequestMessage put = _http.Requests[^1];
            Assert.Equal(HttpMethod.Put, put.Method);
            Assert.Contains("/api/form-specs/d4", put.RequestUri!.AbsolutePath);
            Assert.Contains("status=published", put.RequestUri!.Query);
        }

        [Fact]
        public async Task Publish_NoInProgressDraft_Throws()
        {
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));   // draft latest -> v3
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));   // published latest -> v3 (nothing new)

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NewProvider().PublishAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task Publish_StrapiRefuses_SurfacesTheErrorBody()
        {
            const string errorBody = """{ "error": { "message": "Duplicate component key \"dupe\"", "name": "ApplicationError" } }""";
            _http.Enqueue(Ok(Data(RowWithDoc("f", 4, "d4"))));   // draft latest -> v4
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));           // published latest -> v3
            _http.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json"),
            });

            StrapiWriteException ex = await Assert.ThrowsAsync<StrapiWriteException>(() =>
                NewProvider().PublishAsync("f", CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.Contains("Duplicate component key", ex.Body);
        }

        [Fact]
        public async Task SendsTheConfiguredAdminTokenAsABearerHeader()
        {
            _http.Enqueue(Ok("""{ "data": [] }"""));

            await NewProvider("tok-admin-123").GetDraftAsync("f", CancellationToken.None);

            var auth = _http.Requests[0].Headers.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Equal("tok-admin-123", auth.Parameter);
        }

        [Fact]
        public async Task NoToken_SendsNoAuthorizationHeader()
        {
            _http.Enqueue(Ok("""{ "data": [] }"""));

            await NewProvider(adminToken: null).GetDraftAsync("f", CancellationToken.None);

            Assert.Null(_http.Requests[0].Headers.Authorization);
        }

        private StrapiFormSpecAdminProvider NewProvider(string? adminToken = null)
        {
            var settings = new Dictionary<string, string?> { ["Strapi:BaseUrl"] = "http://strapi.test" };
            if (adminToken is not null)
            {
                settings["Strapi:AdminApiToken"] = adminToken;
            }

            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new StrapiFormSpecAdminProvider(
                NullLogger<StrapiFormSpecAdminProvider>.Instance,
                new HttpClient(_http),
                config);
        }

        private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        private static string Data(params string[] rows) => $$"""{ "data": [ {{string.Join(",", rows)}} ] }""";

        private static string SingleData(string row) => $$"""{ "data": {{row}} }""";

        private static string Row(string formSpecId, int version, string? title) =>
            $$"""{ "documentId": "doc{{formSpecId}}{{version}}", "formSpecId": "{{formSpecId}}", "version": {{version}}, "title": {{Json(title)}} }""";

        private static string RowWithDoc(string formSpecId, int version, string documentId) =>
            $$"""{ "documentId": "{{documentId}}", "formSpecId": "{{formSpecId}}", "version": {{version}}, "title": "t" }""";

        private static string RowWithSpec(string formSpecId, int version, string? title, string specJson) =>
            $$"""{ "documentId": "doc{{formSpecId}}{{version}}", "formSpecId": "{{formSpecId}}", "version": {{version}}, "title": {{Json(title)}}, "spec": {{specJson}} }""";

        private static string Json(string? value) => value is null ? "null" : $"\"{value}\"";

        private static JsonElement Spec(string json) => JsonDocument.Parse(json).RootElement.Clone();

        /// <summary>
        /// Returns queued responses in order and records every request (and body).
        /// </summary>
        private sealed class QueueHttpHandler : HttpMessageHandler
        {
            private readonly Queue<HttpResponseMessage> _responses = new();

            public List<HttpRequestMessage> Requests { get; } = new();

            public List<string?> Bodies { get; } = new();

            public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
                return _responses.Count > 0
                    ? _responses.Dequeue()
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{ "data": [] }""", Encoding.UTF8, "application/json") };
            }
        }
    }
}
