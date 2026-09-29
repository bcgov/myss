namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.Api.Intake;
    using Myss.Api.Platform;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// The worker slice over real HTTP: who may see the list, what is in it,
    /// the three status moves and their rules, and the stream version guard.
    /// </summary>
    public class ReviewEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/review/applications";

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "middleName", "input": true },
            { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private static readonly object Ada = new { firstName = "Ada", lastName = "Lovelace" };

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="ReviewEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public ReviewEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 2, Spec);
            _provider.VersionResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 2, Spec);
        }

        [Fact]
        public async Task TheList_ShowsSubmittedApplicationsNewestFirstAndNeverDrafts()
        {
            using IntakeTestHost host = NewHost();
            Guid first = await host.Create("alice");
            Guid second = await host.Create("bob");
            Guid draft = await host.Create("alice");
            await host.Submit("alice", first, 1, Ada);
            await host.Submit("bob", second, 1, new { firstName = "Bob", lastName = "Byron" });

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, Route);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var rows = (await Payload(response)).EnumerateArray().ToList();
            Assert.Equal(2, rows.Count);
            Assert.DoesNotContain(rows, r => r.GetProperty("id").GetGuid() == draft);
            Assert.Equal(second, rows[0].GetProperty("id").GetGuid());
            Assert.Equal(first, rows[1].GetProperty("id").GetGuid());
            Assert.Equal(ApplicationReference.From(first), rows[1].GetProperty("referenceNumber").GetString());
            Assert.Equal(ApplicationStatusCodes.Submitted, rows[1].GetProperty("status").GetString());
            Assert.Equal(1, rows[1].GetProperty("streamVersion").GetInt32());
        }

        [Fact]
        public async Task TheReferenceNumber_MatchesWhatTheApplicantWasShown()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);

            using HttpResponseMessage mine = await host.Send("alice", HttpMethod.Get, $"/v1/intake/applications/{id}");
            using HttpResponseMessage theirs = await host.Send("worker", HttpMethod.Get, $"{Route}/{id}");

            string? applicantSees = (await Payload(mine)).GetProperty("referenceNumber").GetString();
            string? workerSees = (await Payload(theirs)).GetProperty("referenceNumber").GetString();
            Assert.False(string.IsNullOrWhiteSpace(applicantSees));
            Assert.Equal(applicantSees, workerSees);
        }

        [Fact]
        public async Task AWorkerRead_CarriesTheSubmittedAnswersTheSpecAndTheOpenActions()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, $"{Route}/{id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement app = await Payload(response);
            Assert.Equal("Ada", app.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal(2, app.GetProperty("spec").GetProperty("version").GetInt32());
            Assert.Equal([WorkerActions.Review], Actions(app));
        }

        [Fact]
        public async Task ADraft_DoesNotExistForAWorker()
        {
            using IntakeTestHost host = NewHost();
            Guid draft = await host.Create("alice");

            using HttpResponseMessage get = await host.Send("worker", HttpMethod.Get, $"{Route}/{draft}");
            using HttpResponseMessage review = await host.Send("worker", HttpMethod.Post, $"{Route}/{draft}/review", Version(0));

            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, review.StatusCode);
        }

        [Fact]
        public async Task TheMoves_FollowTheRulesAndRecordTheWorker()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);

            using HttpResponseMessage early = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/accept", Version(1));
            using HttpResponseMessage review = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1));
            using HttpResponseMessage again = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(2));
            using HttpResponseMessage accept = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/accept", Version(2));
            using HttpResponseMessage deny = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/deny", Version(3));

            Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
            Assert.Equal(IntakeKeywords.ReviewNotAllowed, await Keyword(early));
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);
            JsonElement underReview = await Payload(review);
            Assert.Equal(ApplicationStatusCodes.UnderReview, underReview.GetProperty("status").GetString());
            Assert.Equal(2, underReview.GetProperty("streamVersion").GetInt32());
            Assert.Equal([WorkerActions.Accept, WorkerActions.Deny], Actions(underReview));
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal(IntakeKeywords.ReviewNotAllowed, await Keyword(again));
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            JsonElement accepted = await Payload(accept);
            Assert.Equal(ApplicationStatusCodes.Accepted, accepted.GetProperty("status").GetString());
            Assert.Empty(Actions(accepted));
            Assert.Equal(HttpStatusCode.Conflict, deny.StatusCode);
            Assert.Equal(3, await host.CountEvents(id));
            Assert.Equal("worker:MWORKER", await host.LastActor(id));
        }

        [Fact]
        public async Task Deny_IsTerminalToo()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            using HttpResponseMessage review = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1));
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);

            using HttpResponseMessage deny = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/deny", Version(2));
            using HttpResponseMessage accept = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/accept", Version(3));

            Assert.Equal(HttpStatusCode.OK, deny.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Denied, (await Payload(deny)).GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.Conflict, accept.StatusCode);
        }

        [Fact]
        public async Task AStaleStreamVersion_AppendsNothing()
        {
            // Two workers on the same file: the second one's screen is behind.
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            using HttpResponseMessage review = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1));
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);

            using HttpResponseMessage stale = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/accept", Version(1));

            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal(PlatformKeywords.EventStoreConflict, await Keyword(stale));
            Assert.Equal(2, (await Problem(stale)).GetProperty("currentVersion").GetInt32());
            Assert.Equal(2, await host.CountEvents(id));
        }

        [Fact]
        public async Task TheApplicant_SeesTheDecisionReadOnly()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            using HttpResponseMessage review = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1));
            using HttpResponseMessage deny = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/deny", Version(2));
            Assert.Equal(HttpStatusCode.OK, deny.StatusCode);

            using HttpResponseMessage mine = await host.Send("alice", HttpMethod.Get, $"/v1/intake/applications/{id}");
            using HttpResponseMessage save = await host.Send(
                "alice", HttpMethod.Put, $"/v1/intake/applications/{id}/answers", JsonContent.Create(new { version = 1, answers = new { firstName = "Eve" } }));

            JsonElement app = await Payload(mine);
            Assert.Equal(ApplicationStatusCodes.Denied, app.GetProperty("status").GetString());
            Assert.Equal("Ada", app.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(save));
        }

        [Theory]
        [InlineData("alice")]
        [InlineData("workernoidir")]
        public async Task OnlyAWorkerWithIdir_MayReview(string persona)
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);

            using HttpResponseMessage list = await host.Send(persona, HttpMethod.Get, Route);
            using HttpResponseMessage review = await host.Send(persona, HttpMethod.Post, $"{Route}/{id}/review", Version(1));

            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
        }

        [Fact]
        public async Task AnAnonymousCaller_IsUnauthorized()
        {
            using IntakeTestHost host = new(_factory, _provider, [], mockAuth: false);

            using HttpResponseMessage response = await host.Send(persona: null, HttpMethod.Get, Route);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private static JsonContent Version(int streamVersion)
        {
            return JsonContent.Create(new { streamVersion });
        }

        private static string[] Actions(JsonElement app)
        {
            return app.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString() ?? string.Empty).ToArray();
        }

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").Clone();
        }

        private static async Task<JsonElement> Problem(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.Clone();
        }

        private static async Task<string?> Keyword(HttpResponseMessage response)
        {
            return (await Problem(response)).GetProperty("keyword").GetString();
        }

        private IntakeTestHost NewHost()
        {
            return new IntakeTestHost(_factory, _provider, ["alice", "bob"], mockAuth: true);
        }
    }
}
