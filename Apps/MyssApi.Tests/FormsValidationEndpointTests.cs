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
    using Myss.Api.Domain;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Xunit;

    /// <summary>
    /// The test that proves the server is authoritative.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §8.5 of the assessment names this explicitly: "add integration tests that
    /// POST deliberately invalid payloads directly to the API, bypassing the
    /// client entirely — this is the test that proves the server is
    /// authoritative." Every request here goes over real HTTP through the real
    /// pipeline. No Form.io, no browser, no client-side validation of any kind.
    /// </para>
    /// <para>
    /// The spec these run against is supplied by a fake provider rather than
    /// published to Strapi. That is deliberate: it keeps a SIN field out of any
    /// real form until salted hashing at rest is built, since
    /// <c>FormsService</c> currently persists the answers object verbatim.
    /// </para>
    /// </remarks>
    public class FormsValidationEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string FormSpecId = "validation-test-form";

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "number", "key": "monthlyIncome", "input": true },
            { "type": "email", "key": "contactEmail", "input": true },
            { "type": "email", "key": "confirmEmail", "input": true,
              "properties": { "myssMatches": "contactEmail" } },
            { "type": "textfield", "key": "sin", "input": true,
              "properties": { "myssValidator": "sin" } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="FormsValidationEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public FormsValidationEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.VersionResult = FakeFormSpecProvider.Spec(FormSpecId, 1, Spec);
            _provider.LatestResult = FakeFormSpecProvider.Spec(FormSpecId, 1, Spec);
        }

        [Fact]
        public async Task ValidSubmission_IsAccepted()
        {
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 1, new
            {
                firstName = "Ada",
                monthlyIncome = 2000,
                contactEmail = "ada@example.com",
                confirmEmail = "ada@example.com",
                sin = "050082833",
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SubmissionWithAnInvalidSin_IsRejectedWith422()
        {
            // No client-side validation exists in this path, so a rejection here
            // can only have come from the server.
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 1, new
            {
                firstName = "Ada",
                sin = "050082830",
            });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(ValidationKeywords.SinInvalidChecksum, await FirstKeyword(response, "sin"));
        }

        [Fact]
        public async Task SubmissionMissingARequiredField_IsRejectedWith422()
        {
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 1, new { monthlyIncome = 10 });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(ValidationKeywords.FieldRequired, await FirstKeyword(response, "firstName"));
        }

        [Fact]
        public async Task SubmissionWithAFieldTheFormDoesNotHave_IsRejectedWith422()
        {
            // A payload the client could never have produced. This is the shape
            // of a deliberate probe, not a user mistake.
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 1, new
            {
                firstName = "Ada",
                isAdmin = true,
            });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(ValidationKeywords.FieldUnknown, await FirstKeyword(response, "isAdmin"));
        }

        [Fact]
        public async Task SubmissionWithMismatchedEmailConfirmation_IsRejectedWith422()
        {
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 1, new
            {
                firstName = "Ada",
                contactEmail = "ada@example.com",
                confirmEmail = "ada@exampel.com",
            });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(ValidationKeywords.EmailMismatch, await FirstKeyword(response, "confirmEmail"));
        }

        [Fact]
        public async Task SubmissionClaimingAVersionThatDoesNotExist_IsRejectedWith422()
        {
            _provider.VersionResult = null;
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 99, new { firstName = "Ada" });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(ValidationKeywords.VersionUnknown, await FirstKeyword(response, "FormSpecVersion"));
        }

        [Fact]
        public async Task RejectedSubmission_ReportsEveryFailureAtOnce()
        {
            // The WCAG error-summary pattern needs the whole collection in one
            // response; returning the first failure would make a citizen
            // discover their mistakes one submit at a time.
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 1, new
            {
                monthlyIncome = "not a number",
                sin = "1",
                isAdmin = true,
            });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            JsonElement errors = await Payload(response);
            Assert.True(errors.GetArrayLength() >= 4, $"expected at least 4 errors, got {errors.GetArrayLength()}");
        }

        [Fact]
        public async Task EveryReportedError_CarriesFieldKeywordAndMessage()
        {
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 1, new { sin = "1", isAdmin = true });

            foreach (JsonElement error in (await Payload(response)).EnumerateArray())
            {
                Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("field").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("keyword").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
            }
        }

        [Fact]
        public async Task RejectionResponse_DoesNotEchoTheSubmittedSin()
        {
            // A SIN must not travel back in an error body, where it can reach a
            // browser console, a proxy log or a screenshot in a support ticket.
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await Submit(client, 1, new
            {
                firstName = "Ada",
                sin = "050082830",
            });

            Assert.DoesNotContain("050082830", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task SubmitStillRequiresAuthentication()
        {
            // Validation is not a reason to have loosened the [Authorize] on the
            // controller.
            HttpClient client = CreateClient(mockAuth: false);

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"/v1/forms/{FormSpecId}/submissions")
            {
                Content = JsonContent.Create(new { formSpecVersion = 1, answers = new { firstName = "Ada" } }),
            };
            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private static Task<HttpResponseMessage> Submit(HttpClient client, int version, object answers)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post, $"/v1/forms/{FormSpecId}/submissions")
            {
                Content = JsonContent.Create(new { formSpecVersion = version, answers }),
            };
            request.Headers.Add(MockAuthenticationHandler.PersonaHeader, "alice");
            return client.SendAsync(request);
        }

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload");
        }

        private static async Task<string?> FirstKeyword(HttpResponseMessage response, string field)
        {
            foreach (JsonElement error in (await Payload(response)).EnumerateArray())
            {
                if (error.GetProperty("field").GetString() == field)
                {
                    return error.GetProperty("keyword").GetString();
                }
            }

            return null;
        }

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
                        // The InMemory provider needs its own internal EF service
                        // provider, and cannot map JsonDocument the way Npgsql
                        // does — hence the string conversion below.
                        ServiceProvider efProvider = new ServiceCollection()
                            .AddEntityFrameworkInMemoryDatabase()
                            .BuildServiceProvider();
                        // Registered as a factory rather than via AddDbContext:
                        // AddDbContext<TService, TImpl> would register
                        // DbContextOptions<TImpl>, while the context's
                        // constructor takes DbContextOptions<FormsDbContext>.
                        DbContextOptions<FormsDbContext> options =
                            new DbContextOptionsBuilder<FormsDbContext>()
                                .UseInMemoryDatabase(dbName)
                                .UseInternalServiceProvider(efProvider)
                                .Options;

                        services.RemoveAll<DbContextOptions<FormsDbContext>>();
                        services.RemoveAll<FormsDbContext>();
                        services.AddScoped<FormsDbContext>(_ => new InMemoryFormsDbContext(options));

                        services.RemoveAll<IFormSpecProvider>();
                        services.AddSingleton<IFormSpecProvider>(_provider);
                    });
                })
                .CreateClient();
        }
    }
}
