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

            // The submission was kept: it is readable through the forms module.
            using HttpResponseMessage stored = await client.SendAsync(
                Authenticated(HttpMethod.Get, $"/v1/forms/submissions/{submissionId}"));
            Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
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
        public async Task SubmitRequiresAuthentication()
        {
            HttpClient client = CreateClient(mockAuth: false);

            using var request = new HttpRequestMessage(HttpMethod.Post, Route)
            {
                Content = JsonContent.Create(new { formSpecVersion = 1, answers = NewApplicant() }),
            };
            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private static Task<HttpResponseMessage> Submit(HttpClient client, Dictionary<string, object?> answers)
        {
            HttpRequestMessage request = Authenticated(HttpMethod.Post, Route);
            request.Content = JsonContent.Create(new { formSpecVersion = 1, answers });
            return client.SendAsync(request);
        }

        private static HttpRequestMessage Authenticated(HttpMethod method, string route)
        {
            var request = new HttpRequestMessage(method, route);
            request.Headers.Add(MockAuthenticationHandler.PersonaHeader, "alice");
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
