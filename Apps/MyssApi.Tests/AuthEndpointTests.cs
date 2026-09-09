namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.Api.Configuration;
    using Myss.Api.Tests.TestDoubles;
    using Xunit;

    /// <summary>
    /// <c>GET /v1/auth/me</c> is the SPA's single source of effective roles (ADR-0007): the
    /// browser cannot compute them (it cannot see the derive switch today, nor MySS account
    /// state tomorrow), so it asks. These prove the full pipeline over real HTTP — auth
    /// scheme -&gt; claims transformation -&gt; RoleCalculator -&gt; endpoint.
    /// </summary>
    public class AuthEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> factory;

        /// <summary>Initializes a new instance of the <see cref="AuthEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public AuthEndpointTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task UnauthenticatedCallIsRejectedWith401()
        {
            HttpClient client = this.factory
                .WithWebHostBuilder(builder => builder.UseMockAuthSettings(
                    allowMockAuth: "false", environmentName: "test", mockAuth: "false"))
                .CreateClient();

            HttpResponseMessage response = await client.GetAsync("/v1/auth/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ReturnsTheCallerIdentityForACitizenPersona()
        {
            using JsonDocument body = await this.GetMe("alice");
            JsonElement payload = body.RootElement.GetProperty("payload");

            Assert.True(payload.GetProperty("isAuthenticated").GetBoolean());
            Assert.Equal("mock-alice", payload.GetProperty("subject").GetString());
            Assert.Equal(
                "11111111-1111-1111-1111-111111111111",
                payload.GetProperty("bceidGuid").GetString());
            Assert.Equal([MyssRoles.Client], RolesOf(payload));
        }

        [Fact]
        public async Task DerivesClientForABceidShapedTokenWithNoRoles()
        {
            // The "bceid" persona mirrors a real Basic BCeID token: an identity_provider
            // claim and NO roles. CLIENT in the response can only have come from the
            // claims transformation running RoleCalculator inside the real pipeline.
            using JsonDocument body = await this.GetMe("bceid");
            JsonElement payload = body.RootElement.GetProperty("payload");

            Assert.Equal([MyssRoles.Client], RolesOf(payload));
        }

        [Fact]
        public async Task ReturnsWorkerIdentityForAWorkerPersona()
        {
            using JsonDocument body = await this.GetMe("worker");
            JsonElement payload = body.RootElement.GetProperty("payload");

            Assert.Equal([MyssRoles.Worker], RolesOf(payload));
            Assert.Equal("MWORKER", payload.GetProperty("idirUsername").GetString());
        }

        private static string[] RolesOf(JsonElement payload)
        {
            JsonElement roles = payload.GetProperty("roles");
            var values = new string[roles.GetArrayLength()];
            int i = 0;
            foreach (JsonElement role in roles.EnumerateArray())
            {
                values[i++] = role.GetString() ?? string.Empty;
            }

            System.Array.Sort(values, System.StringComparer.Ordinal);
            return values;
        }

        private async Task<JsonDocument> GetMe(string persona)
        {
            HttpClient client = this.factory
                .WithWebHostBuilder(builder => builder.UseMockAuthSettings(
                    allowMockAuth: "true", environmentName: "test", mockAuth: "true"))
                .CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me");
            request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);

            HttpResponseMessage response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
    }
}
