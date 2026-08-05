namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Text;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Tests for <see cref="StrapiFormSpecProvider"/>.
    /// </summary>
    public class StrapiFormSpecProviderTests
    {
        private const string EntryBody = """
            {
              "data": [
                {
                  "id": 2,
                  "documentId": "abc123",
                  "formSpecId": "poc-test-form",
                  "version": 2,
                  "title": "POC test form",
                  "spec": { "display": "form", "components": [ { "key": "firstName" } ] }
                }
              ],
              "meta": { "pagination": { "total": 1 } }
            }
            """;

        private readonly StubHttpHandler _http = new();

        [Fact]
        public async Task GetLatest_MapsTheFirstEntry()
        {
            _http.Body = EntryBody;
            StrapiFormSpecProvider provider = NewProvider();

            FormSpecModel? spec = await provider.GetLatestAsync("poc-test-form", CancellationToken.None);

            Assert.NotNull(spec);
            Assert.Equal("poc-test-form", spec.FormSpecId);
            Assert.Equal(2, spec.Version);
            Assert.Equal("POC test form", spec.Title);
            Assert.Equal("form", spec.Spec.GetProperty("display").GetString());
            Assert.Equal(1, spec.Spec.GetProperty("components").GetArrayLength());
        }

        [Fact]
        public async Task GetLatest_QueriesNewestPublishedVersion()
        {
            _http.Body = EntryBody;
            StrapiFormSpecProvider provider = NewProvider();

            await provider.GetLatestAsync("poc-test-form", CancellationToken.None);

            string query = _http.LastRequest!.RequestUri!.Query;
            Assert.Contains("filters[formSpecId][$eq]=poc-test-form", query);
            Assert.Contains("sort=version:desc", query);
            Assert.Contains("pagination[limit]=1", query);
        }

        [Fact]
        public async Task GetVersion_QueriesTheExactVersion()
        {
            _http.Body = EntryBody;
            StrapiFormSpecProvider provider = NewProvider();

            await provider.GetVersionAsync("poc-test-form", 7, CancellationToken.None);

            string query = _http.LastRequest!.RequestUri!.Query;
            Assert.Contains("filters[formSpecId][$eq]=poc-test-form", query);
            Assert.Contains("filters[version][$eq]=7", query);
            Assert.DoesNotContain("sort=version:desc", query);
        }

        [Fact]
        public async Task GetLatest_EmptyData_ReturnsNull()
        {
            _http.Body = """{ "data": [], "meta": {} }""";
            StrapiFormSpecProvider provider = NewProvider();

            FormSpecModel? spec = await provider.GetLatestAsync("unknown-form", CancellationToken.None);

            Assert.Null(spec);
        }

        [Fact]
        public async Task GetLatest_MissingTitle_MapsNullTitle()
        {
            _http.Body = """
                { "data": [ { "formSpecId": "x", "version": 1, "spec": {} } ] }
                """;
            StrapiFormSpecProvider provider = NewProvider();

            FormSpecModel? spec = await provider.GetLatestAsync("x", CancellationToken.None);

            Assert.NotNull(spec);
            Assert.Null(spec.Title);
        }

        [Fact]
        public async Task GetLatest_UpstreamError_Throws()
        {
            // Upstream errors should throw rather than look like a missing form.
            _http.Status = HttpStatusCode.InternalServerError;
            StrapiFormSpecProvider provider = NewProvider();

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                provider.GetLatestAsync("poc-test-form", CancellationToken.None));
        }

        [Fact]
        public async Task GetLatest_EscapesTheFormSpecId()
        {
            _http.Body = """{ "data": [] }""";
            StrapiFormSpecProvider provider = NewProvider();

            await provider.GetLatestAsync("weird id/&?", CancellationToken.None);

            string query = _http.LastRequest!.RequestUri!.Query;
            Assert.Contains("weird%20id%2F%26%3F", query);
        }

        private StrapiFormSpecProvider NewProvider()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Strapi:BaseUrl"] = "http://strapi.test" })
                .Build();
            return new StrapiFormSpecProvider(NullLogger<StrapiFormSpecProvider>.Instance, new HttpClient(_http), config);
        }

        private sealed class StubHttpHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            public string Body { get; set; } = "{}";

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                var response = new HttpResponseMessage(Status)
                {
                    Content = new StringContent(Body, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }
        }
    }
}
