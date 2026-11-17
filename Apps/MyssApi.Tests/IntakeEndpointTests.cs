namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.Api.Configuration;
    using Myss.Api.Data;
    using Myss.Api.Intake;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;
    using Myss.Data.Intake;
    using Myss.Data.Platform;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// The applicant slice of Application Intake over real HTTP: ownership,
    /// the draft/submit rules, the row version, and the profile gate. These
    /// are the design's day-one invariants as executable assertions.
    /// </summary>
    public class IntakeEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/applications";

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

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="IntakeEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public IntakeEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 2, Spec);
            _provider.VersionResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 2, Spec);
        }

        [Fact]
        public async Task ARegisteredCitizen_CanStartADraft()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            JsonElement app = await Payload(response);
            Assert.Equal(ApplicationStatusCodes.Draft, app.GetProperty("status").GetString());
            Assert.Equal(ApplicationReference.From(app.GetProperty("id").GetGuid()), app.GetProperty("referenceNumber").GetString());
            Assert.Equal(1, app.GetProperty("version").GetInt32());
            Assert.Equal(2, app.GetProperty("formSpecVersion").GetInt32());
            Assert.Equal(JsonValueKind.Null, app.GetProperty("submittedAt").ValueKind);
        }

        [Fact]
        public async Task ACitizenWithoutAProfile_CannotStartAnApplication()
        {
            using IntakeTestHost host = NewHost(registered: []);

            using HttpResponseMessage response = await host.Send("bob", HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(IntakeKeywords.ProfileRequired, await Keyword(response));
            Assert.Equal(0, await host.CountApplications());
        }

        [Fact]
        public async Task AnotherCitizen_GetsNotFoundOnEveryRoute()
        {
            // The legacy attachment bug, encoded for applications: the owner is
            // part of the lookup, so someone else's id does not exist.
            using IntakeTestHost host = NewHost(registered: ["alice", "bob"]);
            Guid id = await host.Create("alice");

            using HttpResponseMessage get = await host.Send("bob", HttpMethod.Get, $"{Route}/{id}");
            using HttpResponseMessage save = await host.Send("bob", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Eve" }));
            using HttpResponseMessage submit = await host.Send("bob", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, new { firstName = "Eve", lastName = "X" }));
            using HttpResponseMessage withdraw = await host.Send("bob", HttpMethod.Post, $"{Route}/{id}/withdraw");

            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, submit.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, withdraw.StatusCode);
            Assert.Equal(0, await host.CountEvents(id));
        }

        [Fact]
        public async Task ADraftSaveMayBeIncomplete_ButASubmitMayNot()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");

            using HttpResponseMessage save = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" }));
            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(2, new { firstName = "Ada" }));

            Assert.Equal(HttpStatusCode.OK, save.StatusCode);
            Assert.Equal(2, (await Payload(save)).GetProperty("version").GetInt32());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, submit.StatusCode);
            Assert.Equal(ValidationKeywords.FieldRequired, await FirstKeyword(submit, "lastName"));
            Assert.Equal(0, await host.CountEvents(id));
        }

        [Fact]
        public async Task ADraftSave_StillRefusesWhatTheFormCouldNotHaveProduced()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");

            using HttpResponseMessage save = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada", isAdmin = true }));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, save.StatusCode);
            Assert.Equal(ValidationKeywords.FieldUnknown, await FirstKeyword(save, "isAdmin"));
        }

        [Fact]
        public async Task AStaleRowVersion_CannotSave()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage first = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" }));
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            using HttpResponseMessage stale = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Eve" }));

            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(stale));
            Assert.Equal(2, (await Problem(stale)).GetProperty("currentVersion").GetInt32());
            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");
            Assert.Equal("Ada", (await Payload(get)).GetProperty("answers").GetProperty("firstName").GetString());
        }

        [Fact]
        public async Task Submit_AppendsOneEventAndMakesTheApplicationReadOnly()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");

            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
            using HttpResponseMessage again = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
            using HttpResponseMessage save = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Eve" }));

            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            JsonElement app = await Payload(submit);
            Assert.Equal(ApplicationStatusCodes.Submitted, app.GetProperty("status").GetString());
            Assert.NotEqual(JsonValueKind.Null, app.GetProperty("submittedAt").ValueKind);
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(again));
            Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(save));
            Assert.Equal(1, await host.CountEvents(id));
        }

        [Fact]
        public async Task AfterSubmit_ReadsServeWhatWasSubmittedEvenIfTheRowChanged()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

            // A stale tab, or anything else, scribbling on the working copy
            // after the fact must not change what the citizen submitted.
            await host.OverwriteRow(id, """{"firstName":"Eve","lastName":"Nobody"}""");

            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");
            JsonElement app = await Payload(get);
            Assert.Equal("Ada", app.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal(2, app.GetProperty("spec").GetProperty("version").GetInt32());
        }

        [Fact]
        public async Task TheList_ShowsOnlyTheCallersApplicationsWithTheirStatus()
        {
            using IntakeTestHost host = NewHost(registered: ["alice", "bob"]);
            Guid submitted = await host.Create("alice");
            Guid draft = await host.Create("alice");
            await host.Create("bob");
            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{submitted}/submit", Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

            using HttpResponseMessage list = await host.Send("alice", HttpMethod.Get, Route);

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var rows = (await Payload(list)).EnumerateArray().ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(draft, rows[0].GetProperty("id").GetGuid());
            Assert.Equal(ApplicationStatusCodes.Draft, rows[0].GetProperty("status").GetString());
            Assert.Equal(submitted, rows[1].GetProperty("id").GetGuid());
            Assert.Equal(ApplicationStatusCodes.Submitted, rows[1].GetProperty("status").GetString());
        }

        [Fact]
        public async Task AWorker_IsForbiddenOnTheApplicantRoutes()
        {
            using IntakeTestHost host = NewHost(registered: []);

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, Route);
            using HttpResponseMessage create = await host.Send("worker", HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        }

        [Fact]
        public async Task AnAnonymousCaller_IsUnauthorized()
        {
            using IntakeTestHost host = NewHost(registered: [], mockAuth: false);

            using HttpResponseMessage response = await host.Send(persona: null, HttpMethod.Get, Route);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Withdrawal_PersistsWithoutRequiringCompleteAnswersOrAnAvailableSpec(bool submitted)
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            if (submitted)
            {
                using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit",
                    Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
                Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            }

            _provider.VersionResult = null;
            using HttpResponseMessage withdraw = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/withdraw");
            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");
            using HttpResponseMessage list = await host.Send("alice", HttpMethod.Get, Route);
            using HttpResponseMessage again = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/withdraw");
            using HttpResponseMessage save = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(submitted ? 2 : 1, new { firstName = "Eve" }));
            using HttpResponseMessage resubmit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(submitted ? 2 : 1, new { firstName = "Eve", lastName = "X" }));

            Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Withdrawn, (await Payload(withdraw)).GetProperty("status").GetString());
            Assert.Equal(ApplicationStatusCodes.Withdrawn, (await Payload(get)).GetProperty("status").GetString());
            Assert.Equal(ApplicationStatusCodes.Withdrawn, (await Payload(list))[0].GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, resubmit.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(again));
            Assert.Equal(submitted ? 2 : 1, await host.CountEvents(id));
        }

        [Theory]
        [InlineData("answers")]
        [InlineData("submit")]
        public async Task ASaveOrSubmitPausedPastItsStatusCheck_CannotChangeAWithdrawnApplication(string action)
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage first = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" }));
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            // Park the write after it has seen an editable draft, withdraw
            // the application, then let the write finish.
            (Task reached, TaskCompletionSource release) = _provider.PauseNextVersionLookup();
            HttpMethod method = action == "submit" ? HttpMethod.Post : HttpMethod.Put;
            Task<HttpResponseMessage> racing = host.Send("alice", method, $"{Route}/{id}/{action}",
                Answers(2, new { firstName = "Eve", lastName = "Lovelace" }));
            await reached.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage withdraw = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/withdraw");
            release.SetResult();
            using HttpResponseMessage late = await racing.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");

            Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(late));
            JsonElement application = await Payload(get);
            Assert.Equal(ApplicationStatusCodes.Withdrawn, application.GetProperty("status").GetString());
            Assert.Equal("Ada", application.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.False(application.GetProperty("answers").TryGetProperty("lastName", out _));
            Assert.Equal(1, await host.CountEvents(id));
        }

        [Fact]
        public async Task ASubmitPausedBetweenItsRowClaimAndItsEvent_LeavesAWithdrawnApplicationsAnswersAlone()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage first = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" }));
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            // Park the submit after its row write, before its event; the
            // withdrawal reads the row it wrote and appends first.
            (Task reached, TaskCompletionSource release) = host.PauseNextAppend();
            Task<HttpResponseMessage> racing = host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit",
                Answers(2, new { firstName = "Eve", lastName = "Lovelace" }));
            await reached.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage withdraw = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/withdraw");
            release.SetResult();
            using HttpResponseMessage late = await racing.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");

            Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
            Assert.Equal(PlatformKeywords.EventStoreConflict, await Keyword(late));
            JsonElement application = await Payload(get);
            Assert.Equal(ApplicationStatusCodes.Withdrawn, application.GetProperty("status").GetString());
            Assert.Equal("Ada", application.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.False(application.GetProperty("answers").TryGetProperty("lastName", out _));
            JsonElement row = await host.RowAnswers(id);
            Assert.Equal("Ada", row.GetProperty("firstName").GetString());
            Assert.False(row.TryGetProperty("lastName", out _));
            Assert.Equal(1, await host.CountEvents(id));
        }

        [Fact]
        public async Task AWithdrawalPausedBetweenItsRowClaimAndItsEvent_RefusesASubmit()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage first = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" }));
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            (Task reached, TaskCompletionSource release) = host.PauseNextAppend();
            Task<HttpResponseMessage> withdrawing = host.Send("alice", HttpMethod.Post, $"{Route}/{id}/withdraw");
            await reached.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit",
                Answers(2, new { firstName = "Eve", lastName = "Lovelace" }));
            release.SetResult();
            using HttpResponseMessage withdraw = await withdrawing.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");

            Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(submit));
            Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
            JsonElement application = await Payload(get);
            Assert.Equal(ApplicationStatusCodes.Withdrawn, application.GetProperty("status").GetString());
            Assert.Equal("Ada", application.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.False((await host.RowAnswers(id)).TryGetProperty("lastName", out _));
            Assert.Equal(1, await host.CountEvents(id));
        }

        [Fact]
        public async Task AWithdrawalAfterASubmitsEventLands_LeavesTheRowAsItWasWithdrawn()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage first = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" }));
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            // Park the submit with its event in the log but its answers not
            // yet copied into the row, and withdraw the now-submitted file.
            (Task reached, TaskCompletionSource release) = host.PauseAfterNextAppend();
            Task<HttpResponseMessage> racing = host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit",
                Answers(2, new { firstName = "Eve", lastName = "Lovelace" }));
            await reached.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage withdraw = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/withdraw");
            release.SetResult();
            using HttpResponseMessage submit = await racing.WaitAsync(TimeSpan.FromSeconds(10));
            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");

            // The submit stands (its event is the record) and the withdrawal
            // follows it; the row was not written after the withdrawal.
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
            JsonElement application = await Payload(get);
            Assert.Equal(ApplicationStatusCodes.Withdrawn, application.GetProperty("status").GetString());
            Assert.Equal("Lovelace", application.GetProperty("answers").GetProperty("lastName").GetString());
            JsonElement row = await host.RowAnswers(id);
            Assert.Equal("Ada", row.GetProperty("firstName").GetString());
            Assert.False(row.TryGetProperty("lastName", out _));
            Assert.Equal(2, await host.CountEvents(id));
        }

        [Fact]
        public async Task Withdrawal_RequiresAnAuthenticatedApplicant()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage worker = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/withdraw");
            Assert.Equal(HttpStatusCode.Forbidden, worker.StatusCode);
            using IntakeTestHost anonymousHost = NewHost(registered: [], mockAuth: false);
            using HttpResponseMessage anonymous = await anonymousHost.Send(null, HttpMethod.Post, $"{Route}/{id}/withdraw");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.Equal(0, await host.CountEvents(id));
        }

        private static JsonContent Answers(int version, object answers)
        {
            return JsonContent.Create(new { version, answers });
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

        private IntakeTestHost NewHost(IReadOnlyList<string> registered, bool mockAuth = true)
        {
            return new IntakeTestHost(_factory, _provider, registered, mockAuth);
        }
    }
}
