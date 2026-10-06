namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.Api.Configuration;
    using Myss.Api.Domain;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// <c>POST /v1/EligibilityRates</c> through the real pipeline (routing, authentication,
    /// the admin policy, model binding and validation), with the content engine faked.
    /// </summary>
    public class EligibilityRatesEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Path = "/v1/EligibilityRates";

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeEligibilityRateAdminProvider _admin = new();
        private readonly FakeEligibilityRateProvider _reader = new();

        /// <summary>Initializes a new instance of the <see cref="EligibilityRatesEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public EligibilityRatesEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task NoToken_Is401()
        {
            HttpResponseMessage response = await Client(mockAuth: false).SendAsync(Post(SeededTable(), persona: null));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(_admin.Saves);
        }

        [Fact]
        public async Task ACitizen_Is403()
        {
            HttpResponseMessage response = await Client().SendAsync(Post(SeededTable(), "alice"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(_admin.Saves);
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("worker")]
        public async Task AnIdirUser_SavesTheTable(string persona)
        {
            HttpResponseMessage response = await Client().SendAsync(Post(SeededTable(), persona));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(_admin.Saves);
            Assert.Equal(1, _reader.InvalidateCacheCalls);
            JsonNode payload = (await response.Content.ReadFromJsonAsync<JsonNode>())!["payload"]!;
            Assert.Equal(2590.5m, (decimal)payload["incomeRows"]![1]!["i"]!);
        }

        [Fact]
        public async Task TextInAnAmount_Is400_AndNothingIsSaved()
        {
            JsonNode table = SeededTable();
            table["incomeRows"]![0]!["b"] = "aaa";

            HttpResponseMessage response = await Client().SendAsync(Post(table, "admin"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_admin.Saves);
        }

        [Fact]
        public async Task ARowWithoutAColumn_Is400_AndNothingIsSaved()
        {
            JsonNode table = SeededTable();
            table["incomeRows"]![6]!.AsObject().Remove("i");

            HttpResponseMessage response = await Client().SendAsync(Post(table, "admin"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_admin.Saves);
        }

        [Fact]
        public async Task ANegativeAmount_Is422_NamingTheCell()
        {
            JsonNode table = SeededTable();
            table["incomeRows"]![1]!["b"] = -0.01m;

            HttpResponseMessage response = await Client().SendAsync(Post(table, "admin"));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            JsonNode error = (await response.Content.ReadFromJsonAsync<JsonNode>())!["payload"]![0]!;
            Assert.Equal("incomeRows.2.b", (string?)error["field"]);
            Assert.Equal(EligibilityRateKeywords.AmountNegative, (string?)error["keyword"]);
            Assert.Empty(_admin.Saves);
        }

        [Fact]
        public async Task ANullRow_Is422_NotAServerError()
        {
            JsonNode table = SeededTable();
            table["incomeRows"]![3] = null;

            HttpResponseMessage response = await Client().SendAsync(Post(table, "admin"));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Empty(_admin.Saves);
        }

        [Fact]
        public async Task AQuotedNumber_IsAccepted()
        {
            // The API's JSON options read numbers from strings, so "1535.5" binds as 1535.5.
            JsonNode table = SeededTable();
            table["incomeRows"]![0]!["g"] = "1535.5";

            HttpResponseMessage response = await Client().SendAsync(Post(table, "admin"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1535.5m, _admin.Saves[0].IncomeRows[0].G);
        }

        [Fact]
        public async Task AContentEngineFailure_Is502()
        {
            _admin.SaveException = new ContentEngineUnavailableException("down");

            HttpResponseMessage response = await Client().SendAsync(Post(SeededTable(), "admin"));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }

        [Fact]
        public async Task AStrapiRateRuleRefusal_Is422_OnTheWholeSave()
        {
            _admin.SaveException = new StrapiWriteException(HttpStatusCode.BadRequest, """
                { "error": { "name": "ApplicationError", "message": "Family size 2, column B must be 0 or more, not -1.",
                  "details": { "keywords": ["RATES.AMOUNT.NEGATIVE"] } } }
                """);

            HttpResponseMessage response = await Client().SendAsync(Post(SeededTable(), "admin"));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            JsonNode error = (await response.Content.ReadFromJsonAsync<JsonNode>())!["payload"]![0]!;
            Assert.Equal("rates", (string?)error["field"]);
            Assert.Equal(EligibilityRateKeywords.ContentEngineRefused, (string?)error["keyword"]);
            Assert.Equal("Family size 2, column B must be 0 or more, not -1.", (string?)error["message"]);
        }

        [Fact]
        public async Task AStrapiBadRequestThatIsNotARateRule_Is502()
        {
            _admin.SaveException = new StrapiWriteException(
                HttpStatusCode.BadRequest, """{ "error": { "name": "ValidationError", "message": "Invalid key" } }""");

            HttpResponseMessage response = await Client().SendAsync(Post(SeededTable(), "admin"));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }

        private static JsonNode SeededTable() =>
            JsonSerializer.SerializeToNode(
                EligibilityRatesTestData.SeededRequest(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        private static HttpRequestMessage Post(JsonNode table, string? persona)
        {
            HttpRequestMessage request = new(HttpMethod.Post, Path) { Content = JsonContent.Create(table) };
            if (persona is not null)
            {
                request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            }

            return request;
        }

        private HttpClient Client(bool mockAuth = true)
        {
            string enabled = mockAuth ? "true" : "false";
            return _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseMockAuthSettings(allowMockAuth: enabled, environmentName: "test", mockAuth: enabled);
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IEligibilityRateAdminProvider>();
                        services.AddSingleton<IEligibilityRateAdminProvider>(_admin);
                        services.RemoveAll<IEligibilityRateProvider>();
                        services.AddSingleton<IEligibilityRateProvider>(_reader);
                    });
                })
                .CreateClient();
        }
    }
}
