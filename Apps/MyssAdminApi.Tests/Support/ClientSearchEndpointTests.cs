namespace Myss.AdminApi.Tests.Support
{
    using System.Net;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.AdminApi.Support;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.AdminApi.Tests.TestSupport;
    using Myss.Data;

    /// <summary>
    /// Support View client search (MYSS-275) over real HTTP: a worker finds
    /// clients by first name, last name, SIN or BCeID user name, alone or
    /// together, and sees enough to tell them apart, with the SIN masked to its
    /// last three digits.
    /// </summary>
    public class ClientSearchEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/support/clients";

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="ClientSearchEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public ClientSearchEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ANamePart_FindsTheClient_IgnoringCase_WithTheSinMasked()
        {
            using CitizenAndStaffTestHost host = NewHost();
            await host.AddProfiles(
                Profile("Ada", "Lovelace", "050 082 833", "ada-guid@bceidbasic", bceidUsername: "ADALOVE"),
                Profile("Grace", "Hopper", "046454286", "grace-guid@bceidbasic"));

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, $"{Route}?lastName=LOVE");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string body = await response.Content.ReadAsStringAsync();
            JsonElement client = Assert.Single(Clients(body));
            Assert.Equal("Ada", client.GetProperty("firstName").GetString());
            Assert.Equal("Lovelace", client.GetProperty("lastName").GetString());
            Assert.Equal("ADALOVE", client.GetProperty("bceidUsername").GetString());
            Assert.Equal("833", client.GetProperty("sinLastThree").GetString());
            Assert.Equal("1990-01-01", client.GetProperty("dateOfBirth").GetString());
            Assert.Equal(ClientSignInTypes.Bceid, client.GetProperty("signInType").GetString());

            // The full SIN never leaves the API.
            Assert.DoesNotContain("050", body, StringComparison.Ordinal);
            Assert.DoesNotContain("082", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SeveralFields_AllHaveToMatch()
        {
            using CitizenAndStaffTestHost host = NewHost();
            await host.AddProfiles(
                Profile("Ada", "Lovelace", "050082833", "a@bceidbasic"),
                Profile("Ada", "Byron", "046454286", "b@bceidbasic"),
                Profile("Augusta", "Lovelace", "130692544", "c@bceidbasic"));

            using HttpResponseMessage response = await host.Send(
                "worker", HttpMethod.Get, $"{Route}?firstName=ada&lastName=lovelace");

            JsonElement client = Assert.Single(Clients(await response.Content.ReadAsStringAsync()));
            Assert.Equal("Ada", client.GetProperty("firstName").GetString());
            Assert.Equal("Lovelace", client.GetProperty("lastName").GetString());
        }

        [Fact]
        public async Task TheFullSin_FindsTheClient_HoweverEitherSideIsPunctuated()
        {
            using CitizenAndStaffTestHost host = NewHost();
            await host.AddProfiles(
                Profile("Ada", "Lovelace", "050 082 833", "a@bceidbasic"),
                Profile("Grace", "Hopper", "046454286", "b@bceidbasic"));

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, $"{Route}?sin=050-082-833");

            JsonElement client = Assert.Single(Clients(await response.Content.ReadAsStringAsync()));
            Assert.Equal("Ada", client.GetProperty("firstName").GetString());
        }

        [Theory]
        [InlineData("833")]
        [InlineData("05008283")]
        [InlineData("0500828333")]
        [InlineData("050O82833")]
        public async Task APartialOrMalformedSin_IsRefused(string sin)
        {
            using CitizenAndStaffTestHost host = NewHost();

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, $"{Route}?sin={sin}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ClientSearchKeywords.SinInvalid, await Keyword(response));
        }

        [Fact]
        public async Task ABceidUsernamePart_FindsTheClient()
        {
            using CitizenAndStaffTestHost host = NewHost();
            await host.AddProfiles(
                Profile("Ada", "Lovelace", "050082833", "a@bceidbasic", bceidUsername: "ADALOVE"),
                Profile("Grace", "Hopper", "046454286", "b@bceidbasic"));

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, $"{Route}?bceidUsername=dalo");

            JsonElement client = Assert.Single(Clients(await response.Content.ReadAsStringAsync()));
            Assert.Equal("ADALOVE", client.GetProperty("bceidUsername").GetString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("?firstName=&lastName=%20%20")]
        public async Task NoCriteria_IsRefused(string query)
        {
            using CitizenAndStaffTestHost host = NewHost();
            await host.AddProfiles(Profile("Ada", "Lovelace", "050082833", "a@bceidbasic"));

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, Route + query);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ClientSearchKeywords.CriteriaRequired, await Keyword(response));
        }

        [Fact]
        public async Task NoMatch_IsAnEmptyList()
        {
            using CitizenAndStaffTestHost host = NewHost();
            await host.AddProfiles(Profile("Ada", "Lovelace", "050082833", "a@bceidbasic"));

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, $"{Route}?lastName=Turing");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string body = await response.Content.ReadAsStringAsync();
            Assert.Empty(Clients(body));
            Assert.False(Payload(body).GetProperty("hasMore").GetBoolean());
        }

        [Fact]
        public async Task ManyMatches_AreCapped_InNameOrder_AndSaySo()
        {
            using CitizenAndStaffTestHost host = NewHost();
            await host.AddProfiles(
                [.. Enumerable.Range(0, ClientSearchService.MaxResults + 5)
                    .Select(i => Profile($"Client{i:D3}", "Smith", "050082833", $"s{i}@bceidbasic"))]);

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, $"{Route}?lastName=smith");

            string body = await response.Content.ReadAsStringAsync();
            JsonElement[] clients = Clients(body);
            Assert.Equal(ClientSearchService.MaxResults, clients.Length);
            Assert.True(Payload(body).GetProperty("hasMore").GetBoolean());
            Assert.Equal("Client000", clients[0].GetProperty("firstName").GetString());
            Assert.Equal("Client049", clients[^1].GetProperty("firstName").GetString());
        }

        [Fact]
        public async Task TheSignInType_ComesFromTheSubjectOrTheOrigin()
        {
            using CitizenAndStaffTestHost host = NewHost();
            string bcscAlias = host.StaffSetting("Oidc:BcServicesCardIdp") ?? host.StaffSetting("Oidc:ClientId")!;
            MyssUserProfile workerOverride = Profile("D", "Sign", "050082833", ProfileOrigins.WorkerOverrideSubject(Guid.NewGuid()));
            workerOverride.Origin = ProfileOrigins.WorkerOverride;
            await host.AddProfiles(
                Profile("A", "Sign", "050082833", "a@bceidboth"),
                Profile("B", "Sign", "050082833", $"b@{bcscAlias}"),
                Profile("C", "Sign", "050082833", "mock-c"),
                workerOverride);

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, $"{Route}?lastName=sign");

            Assert.Equal(
                [ClientSignInTypes.Bceid, ClientSignInTypes.BcServicesCard, null, ClientSignInTypes.WorkerOverride],
                Clients(await response.Content.ReadAsStringAsync()).Select(c => c.GetProperty("signInType").GetString()));
        }

        [Theory]
        [InlineData("alice")]
        [InlineData("workernoidir")]
        public async Task OnlyAWorkerWithIdir_MaySearch(string persona)
        {
            using CitizenAndStaffTestHost host = NewHost(["alice"]);

            using HttpResponseMessage response = await host.Send(persona, HttpMethod.Get, $"{Route}?lastName=Tester");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private static MyssUserProfile Profile(
            string firstName,
            string lastName,
            string sin,
            string subject,
            string? bceidUsername = null) => new()
            {
                Id = Guid.NewGuid(),
                Subject = subject,
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = new DateOnly(1990, 1, 1),
                Email = "client@example.com",
                Sin = sin,
                BceidUsername = bceidUsername,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

        private static JsonElement Payload(string body)
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("payload").Clone();
        }

        private static JsonElement[] Clients(string body) => [.. Payload(body).GetProperty("clients").EnumerateArray()];

        private static async Task<string?> Keyword(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("keyword").GetString();
        }

        private CitizenAndStaffTestHost NewHost(IReadOnlyList<string>? registered = null) =>
            new(_factory, _provider, registered ?? [], mockAuth: true);
    }
}
