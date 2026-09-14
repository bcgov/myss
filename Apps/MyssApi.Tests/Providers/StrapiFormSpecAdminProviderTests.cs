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

            FormSpecModel? spec = await NewProvider().GetDraftOrLatestPublishedAsync("estimator", CancellationToken.None);

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

            FormSpecModel? spec = await NewProvider().GetDraftOrLatestPublishedAsync("nope", CancellationToken.None);

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

            await Assert.ThrowsAsync<NoDraftToPublishException>(() =>
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

            await NewProvider("tok-admin-123").GetDraftOrLatestPublishedAsync("f", CancellationToken.None);

            var auth = _http.Requests[0].Headers.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Equal("tok-admin-123", auth.Parameter);
        }

        [Fact]
        public async Task NoToken_SendsNoAuthorizationHeader()
        {
            _http.Enqueue(Ok("""{ "data": [] }"""));

            await NewProvider(adminToken: null).GetDraftOrLatestPublishedAsync("f", CancellationToken.None);

            Assert.Null(_http.Requests[0].Headers.Authorization);
        }

        [Fact]
        public async Task Write_ConnectionFailure_ThrowsContentEngineUnavailable()
        {
            // SaveDraft's first call is a read; a transport failure there must surface
            // as unavailable (=> 502), not a raw HttpRequestException (=> 500).
            _http.EnqueueThrow(new HttpRequestException("connection refused"));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().SaveDraftAsync("f", Spec("{}"), "t", CancellationToken.None));
        }

        [Fact]
        public async Task Timeout_ThrowsContentEngineUnavailable()
        {
            // HttpClient surfaces a timeout as a TaskCanceledException whose token is
            // not the caller's; with CancellationToken.None it is an upstream failure.
            _http.EnqueueThrow(new TaskCanceledException("timed out"));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task CallerCancellation_Propagates_NotMappedToUnavailable()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            _http.EnqueueThrow(new OperationCanceledException(cts.Token));

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", cts.Token));
        }

        [Fact]
        public async Task Read_NonSuccessStatus_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task Read_MalformedSuccessBody_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("not json {"));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task Read_UnexpectedRowShape_ThrowsContentEngineUnavailable()
        {
            // A 200 whose row is missing required fields is an upstream failure, not a
            // silent mis-parse.
            _http.Enqueue(Ok("""{ "data": [ { "documentId": "d1" } ] }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task Read_MissingDataProperty_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("{}"));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task Read_NonArrayData_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("""{ "data": {} }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task Read_NonObjectRoot_ThrowsContentEngineUnavailable()
        {
            // Previously a non-object root threw InvalidOperationException outside the
            // parse guard (=> 500); it must now be an upstream failure (=> 502).
            _http.Enqueue(Ok("[]"));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task Write_EmptyObjectResponse_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("""{ "data": [] }"""));   // draft latest -> none
            _http.Enqueue(Ok("""{ "data": [] }"""));   // published latest -> none
            _http.Enqueue(Ok("{}"));                     // POST create -> malformed (no data object)

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().SaveDraftAsync("f", Spec("{}"), "t", CancellationToken.None));
        }

        [Fact]
        public async Task Write_NonObjectRootResponse_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("""{ "data": [] }"""));   // draft latest -> none
            _http.Enqueue(Ok("""{ "data": [] }"""));   // published latest -> none
            _http.Enqueue(Ok("[]"));                     // POST create -> array root

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().SaveDraftAsync("f", Spec("{}"), "t", CancellationToken.None));
        }

        [Fact]
        public async Task Write_EmptyDataObject_ThrowsContentEngineUnavailable()
        {
            // A 200 whose entity is {} is not evidence Strapi saved the draft.
            _http.Enqueue(Ok("""{ "data": [] }"""));   // draft latest -> none
            _http.Enqueue(Ok("""{ "data": [] }"""));   // published latest -> none
            _http.Enqueue(Ok("""{ "data": {} }"""));   // POST create -> empty entity

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().SaveDraftAsync("f", Spec("{}"), "t", CancellationToken.None));
        }

        [Fact]
        public async Task Write_WrongTypedTitle_ThrowsContentEngineUnavailable()
        {
            // A wrong-typed echoed field must be a 502, not an uncaught 500.
            _http.Enqueue(Ok("""{ "data": [] }"""));
            _http.Enqueue(Ok("""{ "data": [] }"""));
            _http.Enqueue(Ok("""{ "data": { "formSpecId": "f", "version": 1, "spec": {}, "title": 123 } }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().SaveDraftAsync("f", Spec("{}"), "t", CancellationToken.None));
        }

        [Fact]
        public async Task Publish_EmptyResponse_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok(Data(RowWithDoc("f", 4, "d4"))));   // draft latest -> v4
            _http.Enqueue(Ok(Data(Row("f", 3, "F"))));           // published latest -> v3
            _http.Enqueue(Ok("""{ "data": {} }"""));             // PUT publish -> empty entity

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().PublishAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task GetDraft_ScalarSpec_ThrowsContentEngineUnavailable()
        {
            // An editable draft must have an object-valued spec; a scalar is malformed.
            _http.Enqueue(Ok("""{ "data": [ { "documentId": "d", "formSpecId": "f", "version": 1, "spec": "scalar" } ] }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task GetDraft_NullFormSpecId_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("""{ "data": [ { "documentId": "d", "formSpecId": null, "version": 1, "spec": {} } ] }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public async Task ListForms_PagesBeyondTheHundredRowCap()
        {
            // Strapi caps REST at 100 rows/request; a full first page must trigger a second
            // fetch, or forms/versions past 100 would be silently dropped.
            string fullPage = Data(Enumerable.Range(1, 100).Select(i => Row("form", i, "F")).ToArray());
            _http.Enqueue(Ok(fullPage));                        // published page 1 (full -> more)
            _http.Enqueue(Ok(Data(Row("form", 101, "F"))));     // published page 2 (short -> stop)
            _http.Enqueue(Ok("""{ "data": [] }"""));            // draft page 1 (empty -> stop)

            IReadOnlyList<FormSummaryModel> forms = await NewProvider().ListFormsAsync(CancellationToken.None);

            FormSummaryModel form = Assert.Single(forms);
            Assert.Equal(101, form.Versions.Count);
            Assert.Contains("pagination[page]=1", _http.Requests[0].RequestUri!.Query);
            Assert.Contains("pagination[pageSize]=100", _http.Requests[0].RequestUri!.Query);
            Assert.Contains("pagination[page]=2", _http.Requests[1].RequestUri!.Query);
        }

        [Fact]
        public async Task Write_NonPositiveVersion_ThrowsContentEngineUnavailable()
        {
            // A present version below the schema minimum of 1 is a malformed response.
            _http.Enqueue(Ok("""{ "data": [] }"""));
            _http.Enqueue(Ok("""{ "data": [] }"""));
            _http.Enqueue(Ok("""{ "data": { "formSpecId": "f", "version": 0, "spec": {} } }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().SaveDraftAsync("f", Spec("{}"), "t", CancellationToken.None));
        }

        [Fact]
        public async Task Write_WrongTypedVersion_ThrowsContentEngineUnavailable()
        {
            // A present-but-wrong-type version must be rejected, not masked by the fallback.
            _http.Enqueue(Ok("""{ "data": [] }"""));
            _http.Enqueue(Ok("""{ "data": [] }"""));
            _http.Enqueue(Ok("""{ "data": { "formSpecId": "f", "version": "3", "spec": {} } }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().SaveDraftAsync("f", Spec("{}"), "t", CancellationToken.None));
        }

        [Fact]
        public async Task Write_WrongTypedFormSpecId_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("""{ "data": [] }"""));
            _http.Enqueue(Ok("""{ "data": [] }"""));
            _http.Enqueue(Ok("""{ "data": { "formSpecId": 123, "version": 1, "spec": {} } }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().SaveDraftAsync("f", Spec("{}"), "t", CancellationToken.None));
        }

        [Fact]
        public async Task GetDraft_NonPositiveVersion_ThrowsContentEngineUnavailable()
        {
            _http.Enqueue(Ok("""{ "data": [ { "documentId": "d", "formSpecId": "f", "version": 0, "spec": {} } ] }"""));

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(() =>
                NewProvider().GetDraftOrLatestPublishedAsync("f", CancellationToken.None));
        }

        [Fact]
        public void Constructor_MissingBaseUrl_Throws()
        {
            // A missing Strapi:BaseUrl must fail fast, not silently default to localhost.
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            Assert.Throws<InvalidOperationException>(() =>
                new StrapiFormSpecAdminProvider(
                    NullLogger<StrapiFormSpecAdminProvider>.Instance, new HttpClient(_http), config));
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
            private readonly Queue<Func<HttpResponseMessage>> _steps = new();

            public List<HttpRequestMessage> Requests { get; } = new();

            public List<string?> Bodies { get; } = new();

            public void Enqueue(HttpResponseMessage response) => _steps.Enqueue(() => response);

            public void EnqueueThrow(Exception ex) => _steps.Enqueue(() => throw ex);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
                if (_steps.Count > 0)
                {
                    return _steps.Dequeue()();
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{ "data": [] }""", Encoding.UTF8, "application/json") };
            }
        }
    }
}
