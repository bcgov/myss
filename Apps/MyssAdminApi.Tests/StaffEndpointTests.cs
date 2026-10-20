namespace Myss.AdminApi.Tests
{
    using System.Net;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.Data;

    /// <summary>
    /// The staff reads moved from MyssApi, over real HTTP: who may read stored
    /// submissions and the bus pass PDF, and <c>GET v1/auth/me</c>, which the admin
    /// webclient reads the effective roles from.
    /// </summary>
    public class StaffEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _specProvider = new();
        private DbContextOptions<FormsDbContext>? _forms;

        /// <summary>Initializes a new instance of the <see cref="StaffEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public StaffEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
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
                Authenticated(HttpMethod.Get, $"/v1/bus-pass/submissions/{Guid.NewGuid()}/pdf", persona: "alice"));
            Assert.Equal(HttpStatusCode.Forbidden, asCitizen.StatusCode);

            // A worker with an IDIR identity passes the policy; an unknown id is
            // then simply not found, which proves the gate without rendering.
            using HttpResponseMessage asWorker = await client.SendAsync(
                Authenticated(HttpMethod.Get, $"/v1/bus-pass/submissions/{Guid.NewGuid()}/pdf", persona: "worker"));
            Assert.Equal(HttpStatusCode.NotFound, asWorker.StatusCode);
        }

        [Fact]
        public async Task ListingSubmissions_IsNotAnonymous()
        {
            HttpClient client = CreateClient(mockAuth: false);

            using HttpResponseMessage list = await client.GetAsync("/v1/forms/bc-bus-pass/submissions");

            Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        }

        [Theory]
        [InlineData("alice")]
        [InlineData("workernoidir")]
        public async Task StoredSubmissions_AreForWorkersWithIdirOnly(string persona)
        {
            HttpClient client = CreateClient();
            Guid id = await SeedSubmission();

            using HttpResponseMessage list = await client.SendAsync(
                Authenticated(HttpMethod.Get, "/v1/forms/poc-test-form/submissions", persona));
            using HttpResponseMessage one = await client.SendAsync(
                Authenticated(HttpMethod.Get, $"/v1/forms/submissions/{id}", persona));

            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, one.StatusCode);
        }

        [Fact]
        public async Task AWorker_ReadsAStoredSubmissionWithTheSpecItWasMadeOn()
        {
            HttpClient client = CreateClient();
            Guid id = await SeedSubmission();
            _specProvider.VersionResult = FakeFormSpecProvider.Spec("poc-test-form", 1);

            using HttpResponseMessage list = await client.SendAsync(
                Authenticated(HttpMethod.Get, "/v1/forms/poc-test-form/submissions", "worker"));
            using HttpResponseMessage one = await client.SendAsync(
                Authenticated(HttpMethod.Get, $"/v1/forms/submissions/{id}", "worker"));

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Equal(id, (await Payload(list))[0].GetProperty("id").GetGuid());
            Assert.Equal(HttpStatusCode.OK, one.StatusCode);
            JsonElement submission = await Payload(one);
            Assert.Equal("Ada", submission.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal(1, submission.GetProperty("spec").GetProperty("version").GetInt32());
        }

        [Fact]
        public async Task Me_ReturnsTheEffectiveRolesAndTheIdirUsername()
        {
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await client.SendAsync(
                Authenticated(HttpMethod.Get, "/v1/auth/me", "worker"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement me = await Payload(response);
            Assert.Equal("mock-worker", me.GetProperty("subject").GetString());
            Assert.Equal("MWORKER", me.GetProperty("idirUsername").GetString());
            Assert.Equal([MyssRoles.Worker], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        }

        [Fact]
        public async Task Me_GivesACitizenTokenNoStaffRole()
        {
            // The same calculation as MyssApi's: a citizen-IDP token never keeps
            // WORKER or ADMIN, so the webclient never offers it a staff screen.
            HttpClient client = CreateClient();

            using HttpResponseMessage response = await client.SendAsync(
                Authenticated(HttpMethod.Get, "/v1/auth/me", "bceid"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                [MyssRoles.Client],
                (await Payload(response)).GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        }

        [Fact]
        public async Task Me_IsNotAnonymous()
        {
            HttpClient client = CreateClient(mockAuth: false);

            using HttpResponseMessage response = await client.GetAsync("/v1/auth/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private static HttpRequestMessage Authenticated(HttpMethod method, string route, string persona)
        {
            var request = new HttpRequestMessage(method, route);
            request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            return request;
        }

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").Clone();
        }

        private async Task<Guid> SeedSubmission()
        {
            using var db = new InMemoryFormsDbContext(_forms!);
            var submission = new FormSubmission
            {
                Id = Guid.NewGuid(),
                FormSpecId = "poc-test-form",
                FormSpecVersion = 1,
                Answers = JsonDocument.Parse("""{"firstName":"Ada"}"""),
                SubmittedAt = DateTimeOffset.UtcNow,
            };
            db.FormSubmissions.Add(submission);
            await db.SaveChangesAsync();
            return submission.Id;
        }

        private HttpClient CreateClient(bool mockAuth = true)
        {
            string dbName = Guid.NewGuid().ToString();
            ServiceProvider efProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();
            DbContextOptions<FormsDbContext> options = new DbContextOptionsBuilder<FormsDbContext>()
                .UseInMemoryDatabase(dbName)
                .UseInternalServiceProvider(efProvider)
                .Options;
            _forms = options;

            return _factory
                .WithWebHostBuilder(builder =>
                {
                    string enabled = mockAuth ? "true" : "false";
                    builder.UseMockAuthSettings(allowMockAuth: enabled, environmentName: "test", mockAuth: enabled);
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<DbContextOptions<FormsDbContext>>();
                        services.RemoveAll<FormsDbContext>();
                        services.AddScoped<FormsDbContext>(_ => new InMemoryFormsDbContext(options));

                        services.RemoveAll<IFormSpecProvider>();
                        services.AddSingleton<IFormSpecProvider>(_specProvider);
                    });
                })
                .CreateClient();
        }
    }
}
