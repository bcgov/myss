namespace Myss.Api.Tests
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationParts;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Myss.Api.Configuration;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;
    using Xunit;

    /// <summary>
    /// A test-only controller exercising the policies over real HTTP. It lives in the test
    /// assembly so no protected surface is added to the shipped API before the real endpoints
    /// are built.
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

        /// <summary>Any authenticated caller.</summary>
        /// <returns>The caller's subject.</returns>
        [HttpGet("any")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public string Any() => this.currentUser.User.Subject;

        /// <summary>Citizen-only endpoint.</summary>
        /// <returns>The caller's subject.</returns>
        [HttpGet("client")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = MyssPolicies.Client)]
        public string Client() => this.currentUser.User.Subject;

        /// <summary>Staff-only endpoint.</summary>
        /// <returns>The caller's subject.</returns>
        [HttpGet("worker")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = MyssPolicies.Worker)]
        public string Worker() => this.currentUser.User.Subject;

        /// <summary>Staff endpoint additionally requiring an IDIR identity.</summary>
        /// <returns>The caller's IDIR username.</returns>
        [HttpGet("worker-idir")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = MyssPolicies.WorkerWithIdir)]
        public string WorkerWithIdir() => this.currentUser.User.IdirUsername ?? string.Empty;
    }

    /// <summary>
    /// End-to-end authorization through the real pipeline: mock scheme -> claims -> policy ->
    /// 200 / 401 / 403. This is the check that the middleware order and claim/role types line
    /// up, which unit tests alone cannot prove.
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
        public async Task UnauthenticatedCallIsRejectedWith401()
        {
            HttpClient client = this.CreateBearerClient();

            HttpResponseMessage response = await client.GetAsync("/test-only/any");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AnonymousEndpointsRemainOpenWhenAuthIsEnabled()
        {
            // Enabling authentication must not lock down endpoints that carry no
            // [Authorize]. The health endpoint is always anonymous, so it proves the
            // pipeline still lets unauthenticated requests through.
            HttpClient client = this.CreateBearerClient();

            HttpResponseMessage response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task MockPersonaIsSignedInAndReachesTheEndpoint()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response = await client.SendAsync(Get("/test-only/any", "alice"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("mock-alice", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ClientPersonaIsAllowedOnAClientEndpoint()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response = await client.SendAsync(Get("/test-only/client", "alice"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task WrongRoleIsRejectedWith403()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response = await client.SendAsync(Get("/test-only/worker", "alice"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task WorkerPersonaReachesTheWorkerEndpoint()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response = await client.SendAsync(Get("/test-only/worker", "worker"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("mock-worker", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task AdminSatisfiesTheWorkerPolicy()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response = await client.SendAsync(Get("/test-only/worker", "admin"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task WorkerWithoutIdirIsRejectedByTheHardenedPolicy()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response =
                await client.SendAsync(Get("/test-only/worker-idir", "workernoidir"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task WorkerWithIdirPassesTheHardenedPolicyAndClaimIsReadable()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response =
                await client.SendAsync(Get("/test-only/worker-idir", "worker"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("MWORKER", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UnknownPersonaIsNotSignedIn()
        {
            HttpClient client = this.CreateMockAuthClient();

            HttpResponseMessage response =
                await client.SendAsync(Get("/test-only/any", "nobody"));

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

        [Fact]
        public async Task EveryCitizenPersonaGetsItsOwnSubject()
        {
            HttpClient client = this.CreateMockAuthClient();
            var seen = new HashSet<string>();

            foreach (string persona in new[] { "alice", "bob", "carol" })
            {
                HttpResponseMessage response =
                    await client.SendAsync(Get("/test-only/client", persona));

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(seen.Add(await response.Content.ReadAsStringAsync()));
            }
        }
    }
}
