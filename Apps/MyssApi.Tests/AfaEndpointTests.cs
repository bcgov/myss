namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.Api.Intake;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;
    using Myss.Data.Intake;

    /// <summary>Product-authored AFA forms reuse citizen intake without entering worker review.</summary>
    public class AfaEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/applications";
        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes the endpoint test factory.</summary>
        /// <param name="factory">Test API host.</param>
        public AfaEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AfaRequiresPublishedSpecAndRegisteredCitizen()
        {
            using var host = new IntakeTestHost(_factory, _provider, ["alice"], mockAuth: true);
            using HttpResponseMessage unavailable = await host.Send("alice", HttpMethod.Post, $"{Route}/afa");
            Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
            Assert.Equal(IntakeKeywords.SpecNotPublished, (await Body(unavailable)).GetProperty("keyword").GetString());
            Assert.Equal(0, await host.CountApplications());

            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.AfaFormSpecId, 3, Spec);
            using HttpResponseMessage unregistered = await host.Send("bob", HttpMethod.Post, $"{Route}/afa");
            Assert.Equal(HttpStatusCode.Forbidden, unregistered.StatusCode);
            using HttpResponseMessage worker = await host.Send("worker", HttpMethod.Post, $"{Route}/afa");
            Assert.Equal(HttpStatusCode.Forbidden, worker.StatusCode);
            using HttpResponseMessage created = await host.Send("alice", HttpMethod.Post, $"{Route}/afa");
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            JsonElement application = (await Body(created)).GetProperty("payload");
            Assert.Equal(IntakeService.AfaFormSpecId, application.GetProperty("formSpecId").GetString());
            Assert.Equal(3, application.GetProperty("formSpecVersion").GetInt32());
            Assert.Equal([IntakeService.AfaFormSpecId, IntakeService.AfaFormSpecId], _provider.LatestCalls);
        }

        [Fact]
        public async Task DraftAndSubmissionAppearOnlyToOwner()
        {
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.AfaFormSpecId, 3, Spec);
            _provider.VersionResult = FakeFormSpecProvider.Spec(IntakeService.AfaFormSpecId, 3, Spec);
            using var host = new IntakeTestHost(_factory, _provider, ["alice", "bob"], mockAuth: true);
            using HttpResponseMessage created = await host.Send("alice", HttpMethod.Post, $"{Route}/afa");
            Guid id = (await Body(created)).GetProperty("payload").GetProperty("id").GetGuid();
            using HttpResponseMessage afaRead = await host.Send("alice", HttpMethod.Get, $"{Route}/afa/{id}");
            Assert.Equal(HttpStatusCode.OK, afaRead.StatusCode);

            using HttpResponseMessage incomplete = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", JsonContent.Create(new { version = 1, answers = new { } }));
            Assert.Equal(HttpStatusCode.OK, incomplete.StatusCode);
            using HttpResponseMessage invalid = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", JsonContent.Create(new { version = 2, answers = new { } }));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
            using HttpResponseMessage outsider = await host.Send("bob", HttpMethod.Get, $"{Route}/{id}");
            Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);

            using HttpResponseMessage submitted = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", JsonContent.Create(new { version = 2, answers = new { firstName = "Ada" } }));
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Submitted, (await Body(submitted)).GetProperty("payload").GetProperty("status").GetString());
            using HttpResponseMessage laterSave = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", JsonContent.Create(new { version = 3, answers = new { firstName = "Eve" } }));
            Assert.Equal(HttpStatusCode.Conflict, laterSave.StatusCode);
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 2, Spec);
            Guid pocId = await host.Create("alice");
            using HttpResponseMessage nonAfaRead = await host.Send("alice", HttpMethod.Get, $"{Route}/afa/{pocId}");
            Assert.Equal(HttpStatusCode.NotFound, nonAfaRead.StatusCode);
            using HttpResponseMessage myAfa = await host.Send("alice", HttpMethod.Get, $"{Route}/afa");
            using HttpResponseMessage bobsAfa = await host.Send("bob", HttpMethod.Get, $"{Route}/afa");
            Assert.Equal([id], (await Body(myAfa)).GetProperty("payload").EnumerateArray().Select(row => row.GetProperty("id").GetGuid()));
            Assert.Empty((await Body(bobsAfa)).GetProperty("payload").EnumerateArray());
            using HttpResponseMessage legacyList = await host.Send("alice", HttpMethod.Get, Route);
            Assert.Equal([pocId], (await Body(legacyList)).GetProperty("payload").EnumerateArray().Select(row => row.GetProperty("id").GetGuid()));
            using HttpResponseMessage read = await host.Send("alice", HttpMethod.Get, $"{Route}/afa/{id}");
            Assert.Equal("Ada", (await Body(read)).GetProperty("payload").GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal((IntakeService.AfaFormSpecId, 3), _provider.VersionCalls[0]);

            Assert.Equal(1, await host.CountEvents(id));
            Assert.NotEqual(id, pocId);
        }

        private static async Task<JsonElement> Body(HttpResponseMessage response)
        {
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.Clone();
        }
    }
}
