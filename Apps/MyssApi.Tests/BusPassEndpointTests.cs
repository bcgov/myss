namespace Myss.Api.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.Api.Configuration;
    using Myss.Api.Data;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Xunit;

    /// <summary>
    /// The bus pass submission endpoint over real HTTP, with the middleware
    /// replaced by a fake: what the citizen gets back for each outcome, and
    /// that the submission survives a failed hand-off.
    /// </summary>
    public class BusPassEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/bus-pass/submissions";

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "radio", "key": "applicantCategory", "input": true, "validate": { "required": true } },
            { "type": "checkbox", "key": "eligibilityAcknowledged", "input": true },
            { "type": "radio", "key": "eligibilityCategory", "input": true },
            { "type": "textfield", "key": "socialInsuranceNumber", "input": true },
            { "type": "textfield", "key": "busPassAccountNumber", "input": true },
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "birthDay", "input": true, "validate": { "required": true } },
            { "type": "select", "key": "birthMonth", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "birthYear", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "phoneNumber", "input": true, "validate": { "required": true } },
            { "type": "select", "key": "phoneType", "input": true, "validate": { "required": true } },
            { "type": "select", "key": "preferredCommunication", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "streetAddress1", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "city", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "province", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "postalCode", "input": true, "validate": { "required": true } },
            { "type": "radio", "key": "mailingAddressDifferent", "input": true },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _specProvider = new();
        private readonly FakeBusPassSubmissionProvider _middleware = new();

        /// <summary>Initializes a new instance of the <see cref="BusPassEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public BusPassEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _specProvider.VersionResult = FakeFormSpecProvider.Spec("bc-bus-pass", 1, Spec);
            _specProvider.LatestResult = _specProvider.VersionResult;
        }

        [Fact]
        public async Task AcceptedByIcm_Returns200WithTheReferenceNumber()
        {
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, NewApplicant());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement payload = await Payload(response);
            Assert.Equal("Accepted", payload.GetProperty("outcome").GetString());
            Assert.Equal("1-TEST-0001", payload.GetProperty("referenceNumber").GetString());
            Assert.NotEqual(Guid.Empty, payload.GetProperty("submissionId").GetGuid());
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("keyword").ValueKind);
        }

        [Fact]
        public async Task RejectedByIcm_Returns200WithTheRejectionKeyword()
        {
            _middleware.Outcome = FakeBusPassSubmissionProvider.Rejected("1-ERR-0002", "NO_MATCH", "Contact or Case Match not Found");
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, NewApplicant());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement payload = await Payload(response);
            Assert.Equal("Rejected", payload.GetProperty("outcome").GetString());
            Assert.Equal(BusPassErrorKeywords.Rejected, payload.GetProperty("keyword").GetString());
            Assert.Equal("NO_MATCH", payload.GetProperty("errorCode").GetString());
        }

        [Fact]
        public async Task MiddlewareDown_Returns503WithTheKeywordAndTheStoredSubmissionId()
        {
            _middleware.Failure = new IcmApiUnavailableException("The ICM middleware could not be reached.");
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, NewApplicant());

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            JsonElement problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(BusPassErrorKeywords.IcmUnavailable, problem.GetProperty("keyword").GetString());
            Guid submissionId = problem.GetProperty("submissionId").GetGuid();

            // The submission was kept: staff can read it through the forms module.
            using HttpResponseMessage stored = await client.SendAsync(
                Authenticated(HttpMethod.Get, $"/v1/forms/submissions/{submissionId}", persona: "worker"));
            Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        }

        [Theory]
        [InlineData("ICM.BUSPASS.TIMEOUT", true)]
        [InlineData("ICM.BUSPASS.UNREACHABLE", false)]
        public async Task MiddlewareDown_SaysWhetherIcmMayHaveTheRequest(string keyword, bool mayHaveReachedIcm)
        {
            // A resend after a timeout can file a second service request; after a
            // connection failure it cannot. The citizen is told which it is.
            _middleware.Failure = new IcmApiUnavailableException("Upstream trouble.")
            {
                Keyword = keyword,
                MayHaveReachedIcm = mayHaveReachedIcm,
            };
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, NewApplicant());

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            JsonElement problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(keyword, problem.GetProperty("errorCode").GetString());
            Assert.Equal(mayHaveReachedIcm, problem.GetProperty("mayHaveReachedIcm").GetBoolean());
        }

        [Fact]
        public async Task TooManySubmissionsFromOneAddress_Are429WithTheKeyword()
        {
            // Every accepted submission files a service request in ICM, so the
            // anonymous route is throttled per address (5 per minute by default).
            HttpClient client = CreateClient();

            for (int i = 0; i < 5; i++)
            {
                using HttpResponseMessage allowed = await Submit(client, NewApplicant());
                Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            }

            using HttpResponseMessage rejected = await Submit(client, NewApplicant());

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.NotNull(rejected.Headers.RetryAfter);
            JsonElement problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(BusPassErrorKeywords.RateLimited, problem.GetProperty("keyword").GetString());
            Assert.Equal(5, _middleware.Submitted.Count);
        }

        [Fact]
        public async Task RuleFailure_Returns422AndNothingReachesTheMiddleware()
        {
            HttpClient client = CreateClient();
            var answers = NewApplicant();
            answers.Remove("socialInsuranceNumber");

            using HttpResponseMessage response = await Submit(client, answers);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains(
                (await Payload(response)).EnumerateArray(),
                e => e.GetProperty("keyword").GetString() == BusPassErrorKeywords.IdentifierRequired);
            Assert.Empty(_middleware.Submitted);
        }

        [Fact]
        public async Task RejectionResponse_DoesNotEchoTheSin()
        {
            HttpClient client = CreateClient();
            var answers = NewApplicant();
            answers["birthYear"] = "2030";

            using HttpResponseMessage response = await Submit(client, answers);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.DoesNotContain("046454286", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task SubmitIsPublic_LikeTheLegacyForm()
        {
            // The BC Bus Pass Program links seniors and other non-clients to
            // this form directly; there is no account to sign in with.
            HttpClient client = CreateClient(mockAuth: false);

            using var request = new HttpRequestMessage(HttpMethod.Post, Route)
            {
                Content = JsonContent.Create(new { formSpecVersion = 1, answers = NewApplicant() }),
            };
            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(_middleware.Submitted);
        }

        [Fact]
        public async Task ThePdf_IsNotAnonymous()
        {
            // The id is unguessable, but the PDF carries a name, SIN, date of
            // birth and address; an anonymous route keyed by id must not exist.
            HttpClient client = CreateClient(mockAuth: false);

            using HttpResponseMessage response = await client.GetAsync($"/v1/bus-pass/submissions/{Guid.NewGuid()}/pdf");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ThePdf_IsForStaffNotCitizens()
        {
            HttpClient client = CreateClient();

            using HttpResponseMessage asCitizen = await client.SendAsync(
                Authenticated(HttpMethod.Get, $"/v1/bus-pass/submissions/{Guid.NewGuid()}/pdf"));
            Assert.Equal(HttpStatusCode.Forbidden, asCitizen.StatusCode);

            // A worker with an IDIR identity passes the policy; an unknown id is
            // then simply not found, which proves the gate without rendering.
            using HttpResponseMessage asWorker = await client.SendAsync(
                Authenticated(HttpMethod.Get, $"/v1/bus-pass/submissions/{Guid.NewGuid()}/pdf", persona: "worker"));
            Assert.Equal(HttpStatusCode.NotFound, asWorker.StatusCode);
        }

        [Fact]
        public async Task TheGenericFormsRoutes_DoNotServeTheBusPassFormAnonymously()
        {
            // The spec must stay public, since the citizen has no account to sign
            // in with; listing submissions and storing one through the generic
            // forms route, which skips the bus pass rules and the ICM hand-off,
            // must not be.
            HttpClient client = CreateClient(mockAuth: false);

            using HttpResponseMessage spec = await client.GetAsync("/v1/forms/bc-bus-pass/spec");
            Assert.Equal(HttpStatusCode.OK, spec.StatusCode);

            using HttpResponseMessage list = await client.GetAsync("/v1/forms/bc-bus-pass/submissions");
            Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

            using HttpResponseMessage stored = await client.PostAsJsonAsync(
                "/v1/forms/bc-bus-pass/submissions",
                new { formSpecVersion = 1, answers = NewApplicant() });
            Assert.Equal(HttpStatusCode.Unauthorized, stored.StatusCode);
        }

        private static Task<HttpResponseMessage> Submit(HttpClient client, Dictionary<string, object?> answers)
        {
            HttpRequestMessage request = Authenticated(HttpMethod.Post, Route);
            request.Content = JsonContent.Create(new { formSpecVersion = 1, answers });
            return client.SendAsync(request);
        }

        private static HttpRequestMessage Authenticated(HttpMethod method, string route, string persona = "alice")
        {
            var request = new HttpRequestMessage(method, route);
            request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            return request;
        }

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload");
        }

        private static Dictionary<string, object?> NewApplicant() => new()
        {
            ["applicantCategory"] = "new",
            ["eligibilityAcknowledged"] = true,
            ["eligibilityCategory"] = "over65",
            ["socialInsuranceNumber"] = "046454286",
            ["firstName"] = "Ada",
            ["lastName"] = "Lovelace",
            ["birthDay"] = "10",
            ["birthMonth"] = "12",
            ["birthYear"] = "1950",
            ["phoneNumber"] = "2505550199",
            ["phoneType"] = "home",
            ["preferredCommunication"] = "phone",
            ["streetAddress1"] = "501 Belleville St",
            ["city"] = "Victoria",
            ["province"] = "BC",
            ["postalCode"] = "V8V 1X4",
            ["mailingAddressDifferent"] = "no",
        };

        private HttpClient CreateClient(bool mockAuth = true)
        {
            string dbName = Guid.NewGuid().ToString();
            return _factory
                .WithWebHostBuilder(builder =>
                {
                    string enabled = mockAuth ? "true" : "false";
                    builder.UseMockAuthSettings(
                        allowMockAuth: enabled, environmentName: "test", mockAuth: enabled);

                    builder.ConfigureServices(services =>
                    {
                        ServiceProvider efProvider = new ServiceCollection()
                            .AddEntityFrameworkInMemoryDatabase()
                            .BuildServiceProvider();
                        DbContextOptions<FormsDbContext> options =
                            new DbContextOptionsBuilder<FormsDbContext>()
                                .UseInMemoryDatabase(dbName)
                                .UseInternalServiceProvider(efProvider)
                                .Options;

                        services.RemoveAll<DbContextOptions<FormsDbContext>>();
                        services.RemoveAll<FormsDbContext>();
                        services.AddScoped<FormsDbContext>(_ => new InMemoryFormsDbContext(options));

                        services.RemoveAll<IFormSpecProvider>();
                        services.AddSingleton<IFormSpecProvider>(_specProvider);

                        services.RemoveAll<IBusPassSubmissionProvider>();
                        services.AddSingleton<IBusPassSubmissionProvider>(_middleware);
                    });
                })
                .CreateClient();
        }
    }
}
