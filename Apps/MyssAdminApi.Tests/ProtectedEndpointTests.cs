namespace Myss.AdminApi.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationParts;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Services;
    using Myss.AdminApi.Tests.TestDoubles;
    using Xunit;

    /// <summary>
    /// A test-only controller exercising the AdminIdir policy over real HTTP. It lives in
    /// the test assembly so no extra surface is added to the shipped API.
    /// </summary>
    [ApiController]
    [Route("test-only")]
    public class ProtectedTestController : ControllerBase
    {
        private readonly ICurrentUserAccessor currentUser;

        /// <summary>Initializes a new instance of the <see cref="ProtectedTestController"/> class.</summary>
        /// <param name="currentUser">The injected caller accessor.</param>
        public ProtectedTestController(ICurrentUserAccessor currentUser)
        {
            this.currentUser = currentUser;
        }

        /// <summary>Admin endpoint requiring an IDIR identity.</summary>
        /// <returns>The caller's IDIR username.</returns>
        [HttpGet("admin-idir")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = MyssPolicies.AdminIdir)]
        public string AdminIdir() => this.currentUser.User.IdirUsername ?? string.Empty;
    }

    /// <summary>
    /// End-to-end authorization through the real pipeline: mock scheme -> claims -> policy ->
    /// 200 / 401 / 403. This is the check that the middleware order and claim types line up,
    /// which unit tests alone cannot prove.
    /// </summary>
    public class ProtectedEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> factory;

        /// <summary>Initializes a new instance of the <see cref="ProtectedEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public ProtectedEndpointTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        /// <summary>Host with the mock gate open; every request is a development persona.</summary>
        private HttpClient CreateMockAuthClient() =>
            this.factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseMockAuthSettings(
                        allowMockAuth: "true", environmentName: "test", mockAuth: "true");
                    builder.ConfigureServices(RegisterTestController);
                })
                .CreateClient();

        /// <summary>Host with the real bearer scheme, so an unauthenticated call is a 401.</summary>
        private HttpClient CreateBearerClient() =>
            this.factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseMockAuthSettings(
                        allowMockAuth: "false", environmentName: "test", mockAuth: "false");
                    builder.ConfigureServices(RegisterTestController);
                })
                .CreateClient();

        private static void RegisterTestController(IServiceCollection services)
        {
            services
                .AddControllers()
                .PartManager.ApplicationParts.Add(
                    new AssemblyPart(typeof(ProtectedTestController).Assembly));
        }

        private static HttpRequestMessage Get(string path, string? persona)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (persona is not null)
            {
                request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            }

            return request;
        }

        [Fact]
        public async Task AdminIdirEndpointRejectsACitizenWith403()
        {
            // The check that proves the policy does something [Authorize] alone would not:
            // a signed-in BCeID citizen is refused the admin form editor.
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response = await client.SendAsync(Get("/test-only/admin-idir", "alice"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AdminIdirEndpointAdmitsAnIdirPersona()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response = await client.SendAsync(Get("/test-only/admin-idir", "worker"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("MWORKER", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task AdminIdirEndpointRejectsAnonymousWith401()
        {
            HttpClient client = this.CreateBearerClient();

            HttpResponseMessage response = await client.SendAsync(Get("/test-only/admin-idir", persona: null));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UnknownPersonaIsNotSignedIn()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response =
                await client.SendAsync(Get("/test-only/admin-idir", "nobody"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public void ProductionEnvironmentWithMockFlagsFailsToStart()
        {
            // The deployment-accident guard: the host must refuse to build.
            using var productionFactory = this.factory.WithWebHostBuilder(builder =>
                builder.UseMockAuthSettings(
                    allowMockAuth: "true", environmentName: "production", mockAuth: "true"));

            Assert.ThrowsAny<System.InvalidOperationException>(
                () => productionFactory.CreateClient());
        }
    }
}
