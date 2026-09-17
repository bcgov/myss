namespace Icm.Api.Host.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Icm.Api.Host.Contracts;
    using Icm.Api.Host.Services;
    using Icm.Api.Host.Tests.TestDoubles;
    using Icm.Api.Models;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// The bus pass route over real HTTP, with the client library replaced by a
    /// fake: what MyssApi gets back for each outcome, and what never gets back.
    /// </summary>
    public class BusPassApplicationsEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/bus-pass/applications";

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeBusPassSubmitter _submitter = new();

        public BusPassApplicationsEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Accepted_Returns200WithTheWorkflowAnswer_AndNoEchoedName()
        {
            using HttpClient client = CreateClient();

            using HttpResponseMessage response = await client.PostAsJsonAsync(Route, NewApplication());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement body = await Body(response);
            Assert.Equal("1-TEST-0001", body.GetProperty("applicationNumber").GetString());
            Assert.Equal("SUCCESS", body.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("errorCode").ValueKind);
            Assert.False(body.TryGetProperty("firstName", out _));
            Assert.False(body.TryGetProperty("lastName", out _));
        }

        [Fact]
        public async Task RejectedByIcm_Returns200WithTheErrorCode()
        {
            _submitter.Result = FakeBusPassSubmitter.Rejected("1-ERR-0002", "NO_MATCH", "Contact or Case Match not Found");
            using HttpClient client = CreateClient();

            using HttpResponseMessage response = await client.PostAsJsonAsync(Route, NewApplication());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement body = await Body(response);
            Assert.Equal("NO_MATCH", body.GetProperty("errorCode").GetString());
            Assert.Equal("Contact or Case Match not Found", body.GetProperty("errorMessage").GetString());
            Assert.Equal("1-ERR-0002", body.GetProperty("applicationNumber").GetString());
        }

        [Theory]
        [InlineData(BusPassKeywords.NotConfigured, HttpStatusCode.ServiceUnavailable)]
        [InlineData(BusPassKeywords.TokenUnavailable, HttpStatusCode.ServiceUnavailable)]
        [InlineData(BusPassKeywords.Unreachable, HttpStatusCode.BadGateway)]
        [InlineData(BusPassKeywords.UpstreamError, HttpStatusCode.BadGateway)]
        [InlineData(BusPassKeywords.Timeout, HttpStatusCode.GatewayTimeout)]
        public async Task NoOutcome_ReturnsAProblemWithTheKeyword(string keyword, HttpStatusCode status)
        {
            _submitter.Failure = new IcmUpstreamException(keyword, (int)status, "Something upstream.");
            using HttpClient client = CreateClient();

            using HttpResponseMessage response = await client.PostAsJsonAsync(Route, NewApplication());

            Assert.Equal(status, response.StatusCode);
            JsonElement problem = await Body(response);
            Assert.Equal(keyword, problem.GetProperty("keyword").GetString());
            Assert.Equal("Something upstream.", problem.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task MissingRequestType_Returns400AndNothingReachesIcm()
        {
            using HttpClient client = CreateClient();
            var body = NewApplication();
            body.Remove("requestType");

            using HttpResponseMessage response = await client.PostAsJsonAsync(Route, body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_submitter.Submitted);
        }

        [Fact]
        public async Task UnknownEnumValue_Returns400()
        {
            using HttpClient client = CreateClient();
            var body = NewApplication();
            body["requestType"] = "Teleport";

            using HttpResponseMessage response = await client.PostAsJsonAsync(Route, body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_submitter.Submitted);
        }

        [Fact]
        public async Task EnumsAreReadCaseInsensitively_AndTheSubmissionKeyIsPassedThrough()
        {
            using HttpClient client = CreateClient();
            var body = NewApplication();
            body["requestType"] = "replacement";
            body["phoneType"] = "CELL";
            body["submissionKey"] = "6f1c2a3b-0000-4000-8000-000000000001";

            using HttpResponseMessage response = await client.PostAsJsonAsync(Route, body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            BusPassApplication sent = Assert.Single(_submitter.Submitted);
            Assert.Equal(BusPassRequestType.Replacement, sent.RequestType);
            Assert.Equal(BusPassPhoneType.Cell, sent.PhoneType);
            Assert.Equal("6f1c2a3b-0000-4000-8000-000000000001", sent.SubmissionKey);
        }

        [Fact]
        public async Task TheCallersRequestId_IsEchoedOnTheResponse()
        {
            using HttpClient client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, Route)
            {
                Content = JsonContent.Create(NewApplication()),
            };
            request.Headers.Add(CorrelationIdAccessor.HeaderName, "myss-req-42");

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal("myss-req-42", Assert.Single(response.Headers.GetValues(CorrelationIdAccessor.HeaderName)));
        }

        [Fact]
        public async Task WithoutABearerToken_Returns401()
        {
            using HttpClient client = _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseIcmSettings();
                    builder.UseRealAuthSettings("sdpr-my-ss-6498");
                    builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IBusPassSubmitter>(_submitter)));
                })
                .CreateClient();

            using HttpResponseMessage response = await client.PostAsJsonAsync(Route, NewApplication());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(_submitter.Submitted);
        }

        [Fact]
        public async Task TheOpenApiDocument_NeedsNoToken()
        {
            // The published contract, readable by anyone who can reach the service.
            using HttpClient client = _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseIcmSettings();
                    builder.UseRealAuthSettings("sdpr-my-ss-6498");
                })
                .CreateClient();

            using HttpResponseMessage response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("/v1/bus-pass/applications", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task ARejectedRequest_StillCarriesTheSecurityHeader()
        {
            using HttpClient client = _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseIcmSettings();
                    builder.UseRealAuthSettings("sdpr-my-ss-6498");
                })
                .CreateClient();

            using HttpResponseMessage response = await client.PostAsJsonAsync(Route, NewApplication());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        }

        [Fact]
        public async Task Health_NeedsNoToken()
        {
            using HttpClient client = _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseIcmSettings();
                    builder.UseRealAuthSettings("sdpr-my-ss-6498");
                })
                .CreateClient();

            using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void RealAuthWithNoAllowedClients_RefusesToStart()
        {
            WebApplicationFactory<Startup> factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseIcmSettings();
                builder.UseRealAuthSettings();
            });

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Contains("AllowedClients", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void MissingIcmBaseUrl_RefusesToStart()
        {
            WebApplicationFactory<Startup> factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseMockAuthSettings("true", "local", "true");
            });

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Contains("Icm is not configured", ex.Message, StringComparison.Ordinal);
        }

        private HttpClient CreateClient()
        {
            return _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseIcmSettings();
                    builder.UseMockAuthSettings("true", "local", "true");
                    builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IBusPassSubmitter>(_submitter)));
                })
                .CreateClient();
        }

        private static Dictionary<string, object?> NewApplication() => new()
        {
            ["requestType"] = "NewApplication",
            ["applicantType"] = "Over65",
            ["acknowledgedEligibilityCriteria"] = true,
            ["socialInsuranceNumber"] = "046454286",
            ["firstName"] = "Myss",
            ["lastName"] = "IntegrationTest",
            ["dateOfBirth"] = "1950-01-01",
            ["phoneNumber"] = "2505550199",
            ["phoneType"] = "Home",
            ["preferredContactMethod"] = "Phone",
            ["residentialAddress"] = new Dictionary<string, string?>
            {
                ["line1"] = "501 Belleville St",
                ["city"] = "Victoria",
                ["province"] = "BC",
                ["postalCode"] = "V8V 1X4",
            },
        };

        private static async Task<JsonElement> Body(HttpResponseMessage response)
        {
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        }
    }
}
