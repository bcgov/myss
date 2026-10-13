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
    /// pick-up and assignment, notes, every decision and its rules, the
    /// return-and-resubmit loop, promotion, and the stream version guard.
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
            using HttpResponseMessage deny = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/deny", Version(4));

            Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
            Assert.Equal(IntakeKeywords.ReviewNotAllowed, await Keyword(early));
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);
            JsonElement underReview = await Payload(review);
            Assert.Equal(ApplicationStatusCodes.UnderReview, underReview.GetProperty("status").GetString());
            Assert.Equal("MWORKER", underReview.GetProperty("assignee").GetString());
            Assert.Equal(2, underReview.GetProperty("streamVersion").GetInt32());
            Assert.Equal(
                [WorkerActions.RequestMoreInfo, WorkerActions.Escalate, WorkerActions.Deny, WorkerActions.Accept, WorkerActions.Release],
                Actions(underReview));
            Assert.True(underReview.GetProperty("canAddNote").GetBoolean());
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal(IntakeKeywords.ReviewNotAllowed, await Keyword(again));
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            JsonElement accepted = await Payload(accept);
            Assert.Equal(ApplicationStatusCodes.Accepted, accepted.GetProperty("status").GetString());
            Assert.Empty(Actions(accepted));
            Assert.Equal(JsonValueKind.Null, accepted.GetProperty("assignee").ValueKind);
            Assert.Equal(HttpStatusCode.Conflict, deny.StatusCode);
            Assert.Equal(
                [ApplicationEventTypes.Submitted, ApplicationEventTypes.PickedUp, ApplicationEventTypes.Accepted, ApplicationEventTypes.Promoted],
                await host.EventTypes(id));
            Assert.Equal(ApplicationActors.Promotion, await host.LastActor(id));
        }

        [Fact]
        public async Task OnlyTheAssignee_MayActOrAddNotes()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            using HttpResponseMessage pickUp = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1));
            Assert.Equal(HttpStatusCode.OK, pickUp.StatusCode);

            // MADMIN passes the WorkerWithIdir policy too: a second worker.
            using HttpResponseMessage theirRead = await host.Send("admin", HttpMethod.Get, $"{Route}/{id}");
            using HttpResponseMessage theirPickUp = await host.Send("admin", HttpMethod.Post, $"{Route}/{id}/review", Version(2));
            using HttpResponseMessage theirAccept = await host.Send("admin", HttpMethod.Post, $"{Route}/{id}/accept", Version(2));
            using HttpResponseMessage theirNote = await host.Send("admin", HttpMethod.Post, $"{Route}/{id}/notes", Note(2, "Mine now."));
            using HttpResponseMessage list = await host.Send("admin", HttpMethod.Get, Route);

            JsonElement read = await Payload(theirRead);
            Assert.Equal("MWORKER", read.GetProperty("assignee").GetString());
            Assert.Empty(Actions(read));
            Assert.False(read.GetProperty("canAddNote").GetBoolean());
            foreach (HttpResponseMessage refused in new[] { theirPickUp, theirAccept, theirNote })
            {
                Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
                Assert.Equal(IntakeKeywords.AssignedToAnother, await Keyword(refused));
            }

            JsonElement row = (await Payload(list)).EnumerateArray().Single();
            Assert.Equal("MWORKER", row.GetProperty("assignee").GetString());
            Assert.False(row.GetProperty("inQueue").GetBoolean());
            Assert.Equal(2, await host.CountEvents(id));
        }

        [Fact]
        public async Task TheAssignee_AddsInternalNotesTheApplicantNeverSees()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);

            using HttpResponseMessage beforePickUp = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/notes", Note(1, "Too early."));
            using HttpResponseMessage pickUp = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1));
            using HttpResponseMessage blank = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/notes", Note(2, "   "));
            using HttpResponseMessage note = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/notes", Note(2, "  Income proof checked.  "));
            using HttpResponseMessage mine = await host.Send("alice", HttpMethod.Get, $"/v1/intake/applications/{id}");

            Assert.Equal(HttpStatusCode.Conflict, beforePickUp.StatusCode);
            Assert.Equal(IntakeKeywords.ReviewNotAllowed, await Keyword(beforePickUp));
            Assert.Equal(HttpStatusCode.OK, pickUp.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
            Assert.Equal(HttpStatusCode.OK, note.StatusCode);
            JsonElement app = await Payload(note);
            Assert.Equal(ApplicationStatusCodes.UnderReview, app.GetProperty("status").GetString());
            Assert.Equal(3, app.GetProperty("streamVersion").GetInt32());
            JsonElement written = app.GetProperty("notes").EnumerateArray().Single();
            Assert.Equal("Income proof checked.", written.GetProperty("text").GetString());
            Assert.Equal("MWORKER", written.GetProperty("author").GetString());

            string applicantBody = await mine.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Income proof checked.", applicantBody, StringComparison.Ordinal);
            Assert.DoesNotContain("notes", applicantBody, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RequestMoreInfo_ReturnsItForTheApplicantToEditAndResubmit()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1)));

            using HttpResponseMessage ask = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/request-more-info", Version(2));
            JsonElement returned = await Payload(ask);
            Assert.Equal(HttpStatusCode.OK, ask.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Returned, returned.GetProperty("status").GetString());
            Assert.True(returned.GetProperty("inQueue").GetBoolean());
            Assert.Empty(Actions(returned));

            // The applicant picks up from what they submitted, edits and resubmits.
            using HttpResponseMessage mine = await host.Send("alice", HttpMethod.Get, $"/v1/intake/applications/{id}");
            JsonElement draft = await Payload(mine);
            Assert.Equal(ApplicationStatusCodes.Returned, draft.GetProperty("status").GetString());
            Assert.Equal("Ada", draft.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal(2, draft.GetProperty("version").GetInt32());
            using HttpResponseMessage save = await host.Send(
                "alice", HttpMethod.Put, $"/v1/intake/applications/{id}/answers", JsonContent.Create(new { version = 2, answers = new { firstName = "Augusta" } }));
            Assert.Equal(HttpStatusCode.OK, save.StatusCode);
            using HttpResponseMessage afterSave = await host.Send("alice", HttpMethod.Get, $"/v1/intake/applications/{id}");
            Assert.Equal("Augusta", (await Payload(afterSave)).GetProperty("answers").GetProperty("firstName").GetString());

            using HttpResponseMessage resubmit = await host.Send(
                "alice",
                HttpMethod.Post,
                $"/v1/intake/applications/{id}/submit",
                JsonContent.Create(new { version = 3, answers = new { firstName = "Augusta", lastName = "King" } }));
            Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Resubmitted, (await Payload(resubmit)).GetProperty("status").GetString());

            using HttpResponseMessage saveAgain = await host.Send(
                "alice", HttpMethod.Put, $"/v1/intake/applications/{id}/answers", JsonContent.Create(new { version = 4, answers = new { firstName = "Eve" } }));
            Assert.Equal(HttpStatusCode.Conflict, saveAgain.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(saveAgain));

            // Back in the queue with the new answers, ready to pick up again.
            using HttpResponseMessage theirs = await host.Send("admin", HttpMethod.Get, $"{Route}/{id}");
            JsonElement worker = await Payload(theirs);
            Assert.Equal(ApplicationStatusCodes.Resubmitted, worker.GetProperty("status").GetString());
            Assert.True(worker.GetProperty("inQueue").GetBoolean());
            Assert.Equal("Augusta", worker.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal([WorkerActions.Review], Actions(worker));
            Assert.Equal(
                [ApplicationHistoryCodes.Submit, ApplicationHistoryCodes.PickUp, ApplicationHistoryCodes.RequestMoreInfo, ApplicationHistoryCodes.Resubmit],
                History(worker));
            JsonElement first = worker.GetProperty("history")[0];
            Assert.Equal("APPLICANT", first.GetProperty("actorKind").GetString());
            Assert.Equal(JsonValueKind.Null, first.GetProperty("idir").ValueKind);
            Assert.Equal("MWORKER", worker.GetProperty("history")[1].GetProperty("idir").GetString());
            Assert.Equal(
                [ApplicationEventTypes.Submitted, ApplicationEventTypes.PickedUp, ApplicationEventTypes.MoreInfoRequested, ApplicationEventTypes.Resubmitted],
                await host.EventTypes(id));
        }

        [Fact]
        public async Task AStaleTab_CannotOverwriteOrResubmitAfterAReturn()
        {
            // Two tabs on the draft at row version 1; the first submits, a
            // worker returns the file, and the second tab, still on version 1,
            // tries to save and to submit what it had.
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1)));
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/request-more-info", Version(2)));

            using HttpResponseMessage staleSave = await host.Send(
                "alice", HttpMethod.Put, $"/v1/intake/applications/{id}/answers", JsonContent.Create(new { version = 1, answers = new { firstName = "Old" } }));
            using HttpResponseMessage staleSubmit = await host.Send(
                "alice",
                HttpMethod.Post,
                $"/v1/intake/applications/{id}/submit",
                JsonContent.Create(new { version = 1, answers = new { firstName = "Old", lastName = "Draft" } }));
            using HttpResponseMessage mine = await host.Send("alice", HttpMethod.Get, $"/v1/intake/applications/{id}");

            Assert.Equal(HttpStatusCode.Conflict, staleSave.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(staleSave));
            Assert.Equal(HttpStatusCode.Conflict, staleSubmit.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(staleSubmit));
            JsonElement app = await Payload(mine);
            Assert.Equal(ApplicationStatusCodes.Returned, app.GetProperty("status").GetString());
            Assert.Equal("Ada", app.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal(3, await host.CountEvents(id));
        }

        [Fact]
        public async Task ThePromotionStep_IsNotTiedToTheAcceptRequest()
        {
            // Once the Accept is in the log, a caller going away must not
            // abort the promotion and leave no outcome behind.
            var promotion = new FakeApplicationPromotionProvider();
            using IntakeTestHost host = new(_factory, _provider, ["alice"], mockAuth: true, promotion);
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1)));

            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/accept", Version(2)));

            Assert.False(Assert.Single(promotion.Tokens).CanBeCanceled);
        }

        [Fact]
        public async Task Escalate_LeavesItReadOnlyForEveryWorker()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1)));

            using HttpResponseMessage escalate = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/escalate", Version(2));
            using HttpResponseMessage theirs = await host.Send("admin", HttpMethod.Get, $"{Route}/{id}");

            JsonElement app = await Payload(escalate);
            Assert.Equal(ApplicationStatusCodes.Escalated, app.GetProperty("status").GetString());
            Assert.Empty(Actions(app));
            Assert.False(app.GetProperty("inQueue").GetBoolean());
            Assert.Empty(Actions(await Payload(theirs)));
            Assert.Equal(ApplicationHistoryCodes.Escalate, History(app)[^1]);
        }

        [Fact]
        public async Task Release_PutsItBackForAnyWorkerToPickUp()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1)));

            using HttpResponseMessage release = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/release", Version(2));
            using HttpResponseMessage theirs = await host.Send("admin", HttpMethod.Post, $"{Route}/{id}/review", Version(3));

            JsonElement released = await Payload(release);
            Assert.Equal(ApplicationStatusCodes.Submitted, released.GetProperty("status").GetString());
            Assert.True(released.GetProperty("inQueue").GetBoolean());
            Assert.Equal(HttpStatusCode.OK, theirs.StatusCode);
            Assert.Equal("MADMIN", (await Payload(theirs)).GetProperty("assignee").GetString());
        }

        [Fact]
        public async Task AFailedPromotion_IsItsOwnStatusAndTheAcceptStands()
        {
            var promotion = new FakeApplicationPromotionProvider { Result = Myss.Api.Providers.PromotionResult.Failed("Case system unavailable.") };
            using IntakeTestHost host = new(_factory, _provider, ["alice"], mockAuth: true, promotion);
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1)));

            using HttpResponseMessage accept = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/accept", Version(2));
            using HttpResponseMessage mine = await host.Send("alice", HttpMethod.Get, $"/v1/intake/applications/{id}");

            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            JsonElement app = await Payload(accept);
            Assert.Equal(ApplicationStatusCodes.PromotionFailed, app.GetProperty("status").GetString());
            Assert.Empty(Actions(app));
            Assert.Equal([ApplicationHistoryCodes.Accept, ApplicationHistoryCodes.PromotionFailed], History(app)[^2..]);
            Assert.Equal("SYSTEM", app.GetProperty("history").EnumerateArray().Last().GetProperty("actorKind").GetString());
            Assert.Equal(ApplicationStatusCodes.PromotionFailed, (await Payload(mine)).GetProperty("status").GetString());
            PromotionRequestAssert(promotion, id);
        }

        [Fact]
        public async Task APromotionThatThrows_IsRecordedAsFailed()
        {
            var promotion = new FakeApplicationPromotionProvider { Throws = new HttpRequestException("boom") };
            using IntakeTestHost host = new(_factory, _provider, ["alice"], mockAuth: true, promotion);
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, Ada);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/review", Version(1)));

            using HttpResponseMessage accept = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/accept", Version(2));

            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            Assert.Equal(ApplicationStatusCodes.PromotionFailed, (await Payload(accept)).GetProperty("status").GetString());
        }

        [Fact]
        public async Task TheQueueFlag_MarksSubmittedResubmittedAndReturnedOnly()
        {
            using IntakeTestHost host = NewHost();
            Guid waiting = await host.Create("alice");
            Guid returned = await host.Create("alice");
            Guid denied = await host.Create("bob");
            await host.Submit("alice", waiting, 1, Ada);
            await host.Submit("alice", returned, 1, Ada);
            await host.Submit("bob", denied, 1, new { firstName = "Bob", lastName = "Byron" });
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{returned}/review", Version(1)));
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{returned}/request-more-info", Version(2)));
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{denied}/review", Version(1)));
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{denied}/deny", Version(2)));

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, Route);

            var rows = (await Payload(list)).EnumerateArray().ToDictionary(r => r.GetProperty("id").GetGuid());
            Assert.True(rows[waiting].GetProperty("inQueue").GetBoolean());
            Assert.True(rows[returned].GetProperty("inQueue").GetBoolean());
            Assert.Equal(ApplicationStatusCodes.Returned, rows[returned].GetProperty("status").GetString());
            Assert.False(rows[denied].GetProperty("inQueue").GetBoolean());
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
            Assert.Equal(ApplicationHistoryCodes.Deny, History(await Payload(deny))[^1]);
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

        private static JsonContent Note(int streamVersion, string text)
        {
            return JsonContent.Create(new { streamVersion, text });
        }

        private static async Task Ok(Task<HttpResponseMessage> send)
        {
            using HttpResponseMessage response = await send;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private static string[] History(JsonElement app)
        {
            return app.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("code").GetString() ?? string.Empty).ToArray();
        }

        private static void PromotionRequestAssert(FakeApplicationPromotionProvider promotion, Guid id)
        {
            Myss.Api.Providers.PromotionRequest request = Assert.Single(promotion.Requests);
            Assert.Equal(id, request.ApplicationId);
            Assert.Equal(ApplicationReference.From(id), request.ReferenceNumber);
            Assert.Equal("Ada", request.Answers.GetProperty("firstName").GetString());
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
