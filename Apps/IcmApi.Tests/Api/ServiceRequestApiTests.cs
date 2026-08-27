namespace Icm.Api.Tests.Api
{
    using System.Net;
    using Icm.Api;
    using Icm.Api.Contracts;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// Checks the requests <see cref="IServiceRequestApi"/> actually puts on the wire
    /// against <c>docs/integration/SR_OpenApi.json</c>. Refit generates its implementation at compile
    /// time, so the interesting failures — a dropped query parameter, a mangled path, a
    /// body full of nulls — are invisible until something inspects a real
    /// <see cref="HttpRequestMessage"/>.
    /// </summary>
    public class ServiceRequestApiTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static (IServiceRequestApi Api, RecordingHttpMessageHandler Handler) CreateApi(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = "{}")
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (RestService.For<IServiceRequestApi>(httpClient, IcmRefitSettings.Create()), handler);
        }

        [Fact]
        public async Task SearchAsync_SendsTheBearerTokenAsAnAuthorizationHeader()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("token-abc", null, new SiebelListQuery());

            Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
            Assert.Equal("token-abc", handler.Request.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task TheTrustedUserNameIsSentAsAnIcmHeader()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", "IDIR\\SOMEONE", new SiebelListQuery());

            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request!.Headers.GetValues(IServiceRequestApi.TrustedUserNameHeader)));
        }

        [Fact]
        public async Task NoTrustedUserNameMeansNoHeaderAtAll()
        {
            // Not an empty header: Refit drops a null-valued one, so a caller that has no
            // ICM user to name sends nothing rather than something ICM has to interpret.
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, new SiebelListQuery());

            Assert.False(handler.Request!.Headers.Contains(IServiceRequestApi.TrustedUserNameHeader));
        }

        [Fact]
        public async Task SearchAsync_KeepsTheCollectionPathAndItsTrailingSlash()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, new SiebelListQuery());

            Assert.Equal(
                "/gov/v1.0/data/ServiceRequest/ServiceRequest/",
                handler.Request!.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task SearchAsync_DefaultsUniformResponse_BecauseTheSpecRequiresIt()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, new SiebelListQuery());

            Assert.Contains("uniformresponse=Y", handler.Request!.RequestUri!.Query, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_FlattensTheQueryObjectUsingTheNamesSiebelExpects()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync(
                "t",
                null,
                new SiebelListQuery
                {
                    SearchSpec = "[SR Number] = \"1-12345\"",
                    SortSpec = "Created",
                    Fields = "SR Number,Status",
                    ChildLinks = "None",
                    PageSize = 25,
                    StartRowNum = 50,
                    Pagination = SiebelFlag.No,
                    ViewMode = "Organization",
                    ExecutionMode = "ForwardOnly",
                });

            string query = handler.Request!.RequestUri!.Query;
            Assert.Contains("sortspec=Created", query, StringComparison.Ordinal);
            Assert.Contains("PageSize=25", query, StringComparison.Ordinal);
            Assert.Contains("StartRowNum=50", query, StringComparison.Ordinal);
            Assert.Contains("pagination=N", query, StringComparison.Ordinal);
            Assert.Contains("ViewMode=Organization", query, StringComparison.Ordinal);
            Assert.Contains("ExecutionMode=ForwardOnly", query, StringComparison.Ordinal);

            // The property name must never leak into the key: Siebel reads `searchspec`,
            // not `query.searchspec`.
            Assert.DoesNotContain("query.", query, StringComparison.Ordinal);

            string decoded = Uri.UnescapeDataString(query);
            Assert.Contains("searchspec=[SR Number] = \"1-12345\"", decoded, StringComparison.Ordinal);
            Assert.Contains("fields=SR Number,Status", decoded, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_OmitsUnsetQueryParametersEntirely()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, new SiebelListQuery());

            // Only the one parameter that carries a default should be present; anything
            // else sent as an empty value would override a Siebel default with nothing.
            Assert.Equal("?uniformresponse=Y", handler.Request!.RequestUri!.Query);
        }

        [Theory]
        [InlineData(true, "true")]
        [InlineData(false, "false")]
        public async Task SearchAsync_RendersBooleansLowerCaseForSiebel(bool value, string expected)
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync(
                "t",
                null,
                new SiebelListQuery
                {
                    RecordCountNeeded = value,
                    ExcludeEmptyFieldsInResponse = value,
                });

            string query = handler.Request!.RequestUri!.Query;
            Assert.Contains($"recordcountneeded={expected}", query, StringComparison.Ordinal);
            Assert.Contains($"excludeEmptyFieldsInResponse={expected}", query, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetAsync_PutsTheKeyInThePath()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetAsync("t", null, "1-ABCDE", new SiebelItemQuery());

            Assert.Equal(
                "/gov/v1.0/data/ServiceRequest/ServiceRequest/1-ABCDE/",
                handler.Request!.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task CreateAsync_SendsOnlyTheFieldsThatWereSet()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.CreateAsync(
                "t",
                null,
                new SiebelServiceRequest
                {
                    SRType = "Application",
                    Status = "Open",
                    ContactCellNumber = "250-555-0100",
                });

            // The record has fifty-odd nullable fields. If nulls were serialized, this PUT
            // would blank every one of them in Siebel.
            Assert.Equal(
                """{"Contact Cell #":"250-555-0100","SR Type":"Application","Status":"Open"}""",
                handler.RequestBody);
        }

        [Fact]
        public async Task UpdateAsync_UsesPutAgainstTheKeyedPath()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.UpdateAsync("t", null, "1-ABCDE", new SiebelServiceRequest { Status = "Closed" }, true);

            Assert.Equal(HttpMethod.Put, handler.Request!.Method);
            Assert.Equal(
                "/gov/v1.0/data/ServiceRequest/ServiceRequest/1-ABCDE/",
                handler.Request.RequestUri!.AbsolutePath);
            Assert.Equal("?excludeEmptyFieldsInResponse=true", handler.Request.RequestUri.Query);
            Assert.Equal("""{"Status":"Closed"}""", handler.RequestBody);
        }

        [Fact]
        public async Task DeleteAsync_UsesDeleteAgainstTheKeyedPath()
        {
            (IServiceRequestApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.DeleteAsync("t", null, "1-ABCDE");

            Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
            Assert.Equal(
                "/gov/v1.0/data/ServiceRequest/ServiceRequest/1-ABCDE/",
                handler.Request.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task SearchAsync_ReportsAnEmptyResultAs204_NotAnException()
        {
            (IServiceRequestApi api, _) = CreateApi(HttpStatusCode.NoContent, responseJson: null);

            IApiResponse<SiebelListResponse> response =
                await api.SearchAsync("t", null, new SiebelListQuery());

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Null(response.Content);
        }

        [Fact]
        public async Task GetAsync_ReportsA404OnTheResponse_RatherThanThrowing()
        {
            (IServiceRequestApi api, _) = CreateApi(HttpStatusCode.NotFound, responseJson: null);

            IApiResponse<SiebelServiceRequest> response =
                await api.GetAsync("t", null, "1-NOPE", new SiebelItemQuery());

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.NotNull(response.Error);
        }
    }
}
