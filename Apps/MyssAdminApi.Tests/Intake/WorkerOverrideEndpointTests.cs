namespace Myss.AdminApi.Tests.Intake
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.AdminApi.Tests.TestSupport;
    using Myss.Data;
    using Myss.Data.Intake;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Worker override (MYSS-321) over real HTTP: a worker starts the simplified
    /// Application for Assistance for a client, any worker may open and save the
    /// draft, and the submit makes the client's worker override account and puts
    /// the application in the worker queue, with the worker as its actor.
    /// </summary>
    public class WorkerOverrideEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/worker-override/applications";
        private const string QueueRoute = "/v1/intake/review/applications";
        private const string DraftsRoute = "/v1/intake/worker-override/applications/drafts";

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
            { "type": "email", "key": "email", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "phone", "input": true, "validate": { "required": true }, "properties": { "myssValidator": "phone" } },
            { "type": "datetime", "key": "dateOfBirth", "input": true, "validate": { "required": true }, "properties": { "myssValidator": "date" } },
            { "type": "textfield", "key": "sin", "input": true, "validate": { "required": true }, "properties": { "myssValidator": "sin" } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        // Synthetic: the SIN passes Luhn and belongs to no one.
        private static readonly object Client = new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = " ada@example.com ",
            phone = "(250) 555-0199",
            dateOfBirth = "1990-01-01",
            sin = "050 082 833",

            // Form.io posts its button's value with the answers.
            submit = true,
        };

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="WorkerOverrideEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public WorkerOverrideEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeFormSpecIds.WorkerOverrideAfa, 1, Spec);
            _provider.VersionResult = FakeFormSpecProvider.Spec(IntakeFormSpecIds.WorkerOverrideAfa, 1, Spec);
        }

        [Fact]
        public async Task AWorker_StartsADraftOnThePublishedForm()
        {
            using CitizenAndStaffTestHost host = NewHost();

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            JsonElement draft = await Payload(response);
            Assert.Equal(ApplicationStatusCodes.Draft, draft.GetProperty("status").GetString());
            Assert.Equal(1, draft.GetProperty("version").GetInt32());
            Assert.Equal(IntakeFormSpecIds.WorkerOverrideAfa, draft.GetProperty("formSpecId").GetString());
            Assert.Equal(1, draft.GetProperty("spec").GetProperty("version").GetInt32());
            Assert.Equal(JsonValueKind.Null, draft.GetProperty("accountId").ValueKind);
            Assert.Empty(await host.Profiles());
        }

        [Fact]
        public async Task WithNoPublishedForm_ThereIsNothingToStart()
        {
            _provider.LatestResult = null;
            using CitizenAndStaffTestHost host = NewHost();

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(IntakeKeywords.SpecNotPublished, await Keyword(response));
        }

        [Fact]
        public async Task AnyWorker_MayOpenAndSaveADraft()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);

            // Half done is fine for a draft: the required and format checks are submit's.
            using HttpResponseMessage save = await host.Send(
                "admin", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada", phone = "250" }));
            using HttpResponseMessage read = await host.Send("worker", HttpMethod.Get, $"{Route}/{id}");

            Assert.Equal(HttpStatusCode.OK, save.StatusCode);
            Assert.Equal(2, (await Payload(save)).GetProperty("version").GetInt32());
            JsonElement draft = await Payload(read);
            Assert.Equal("Ada", draft.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal(2, draft.GetProperty("version").GetInt32());
            Assert.Equal(1, draft.GetProperty("spec").GetProperty("version").GetInt32());
        }

        [Fact]
        public async Task AStaleSave_DoesNotOverwriteAnotherWorkers()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);
            await Ok(host.Send("worker", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" })));

            using HttpResponseMessage stale = await host.Send(
                "admin", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Eve" }));

            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(stale));
            using HttpResponseMessage read = await host.Send("worker", HttpMethod.Get, $"{Route}/{id}");
            Assert.Equal("Ada", (await Payload(read)).GetProperty("answers").GetProperty("firstName").GetString());
        }

        [Theory]
        [InlineData("answers")]
        [InlineData("submit")]
        public async Task AWriteThatLandsBetweenTheCheckAndTheSave_IsAConflict(string action)
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);

            // The spec lookup sits between the version check and the save:
            // another worker's save lands there.
            _provider.OnNextGetVersion = () => host.BumpVersion(id);
            HttpMethod method = action == "submit" ? HttpMethod.Post : HttpMethod.Put;
            using HttpResponseMessage response = await host.Send("worker", method, $"{Route}/{id}/{action}", Answers(1, Client));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(response));
            Assert.Empty(await host.Profiles());
            Assert.Equal(0, await host.CountEvents(id));
        }

        [Fact]
        public async Task AStaleSubmit_IsAConflict_AndMakesNoAccount()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);
            await Ok(host.Send("admin", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" })));

            using HttpResponseMessage submit = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, Client));

            Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(submit));
            Assert.Empty(await host.Profiles());
            Assert.Equal(0, await host.CountEvents(id));
        }

        [Fact]
        public async Task WhenTheDraftsSpecVersionCannotBeLoaded_SaveAndSubmitAreABadGateway()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);
            _provider.VersionResult = null;

            using HttpResponseMessage save = await host.Send("worker", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, Client));
            using HttpResponseMessage submit = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, Client));

            Assert.Equal(HttpStatusCode.BadGateway, save.StatusCode);
            Assert.Equal(HttpStatusCode.BadGateway, submit.StatusCode);
            Assert.Empty(await host.Profiles());
            Assert.Equal(0, await host.CountEvents(id));
        }

        [Fact]
        public async Task ASaveWithAFieldTheFormDoesNotHave_IsRefused()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);

            using HttpResponseMessage save = await host.Send(
                "worker", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { pin = "1234" }));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, save.StatusCode);
            Assert.Equal(ValidationKeywords.FieldUnknown, Assert.Single(Errors(await Payload(save))).Keyword);
        }

        [Fact]
        public async Task TheSubmit_MakesTheClientsWorkerOverrideAccount()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);

            using HttpResponseMessage submit = await host.Send("worker", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, Client));

            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            JsonElement submitted = await Payload(submit);
            Assert.Equal(ApplicationStatusCodes.Submitted, submitted.GetProperty("status").GetString());

            MyssUserProfile account = Assert.Single(await host.Profiles());
            Assert.Equal(account.Id, submitted.GetProperty("accountId").GetGuid());
            Assert.Equal(ProfileOrigins.WorkerOverride, account.Origin);
            Assert.Equal("MWORKER", account.CreatedByIdir);
            Assert.StartsWith(ProfileOrigins.WorkerOverrideSubjectPrefix, account.Subject, StringComparison.Ordinal);
            Assert.Equal("Ada", account.FirstName);
            Assert.Equal("Lovelace", account.LastName);
            Assert.Equal(new DateOnly(1990, 1, 1), account.DateOfBirth);
            Assert.Equal("ada@example.com", account.Email);
            Assert.Equal("2505550199", account.Phone);
            Assert.Equal("050082833", account.Sin);
            MyssUserPhone phone = Assert.Single(account.Phones);
            Assert.Equal(AccountPhoneType.Cell, phone.Type);
            Assert.Equal("2505550199", phone.Number);

            // Nobody has proven the address, signs in or was matched in ICM.
            Assert.Null(account.EmailVerifiedAt);
            Assert.Null(account.EmailVerificationTokenHash);
            Assert.Null(account.PinHash);
            Assert.Null(account.IcmContactMatchedAt);

            using HttpResponseMessage read = await host.Send("admin", HttpMethod.Get, $"{Route}/{id}");
            Assert.Equal(account.Id, (await Payload(read)).GetProperty("accountId").GetGuid());
        }

        [Fact]
        public async Task TheSubmittedApplication_GoesToTheWorkerQueue_WithTheWorkerAsItsActor()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, Client)));

            using HttpResponseMessage list = await host.Send("admin", HttpMethod.Get, QueueRoute);
            using HttpResponseMessage read = await host.Send("admin", HttpMethod.Get, $"{QueueRoute}/{id}");

            JsonElement row = Assert.Single((await Payload(list)).EnumerateArray());
            Assert.Equal(id, row.GetProperty("id").GetGuid());
            Assert.True(row.GetProperty("inQueue").GetBoolean());
            JsonElement app = await Payload(read);
            Assert.Equal("Ada", app.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal([WorkerActions.Review], Actions(app));
            JsonElement submit = Assert.Single(app.GetProperty("history").EnumerateArray());
            Assert.Equal("WORKER", submit.GetProperty("actorKind").GetString());
            Assert.Equal("MWORKER", submit.GetProperty("idir").GetString());
            Assert.Equal(ApplicationActors.Worker("MWORKER"), await host.LastActor(id));
        }

        [Fact]
        public async Task ADraft_IsNotInTheWorkerQueue()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, QueueRoute);
            using HttpResponseMessage read = await host.Send("worker", HttpMethod.Get, $"{QueueRoute}/{id}");

            Assert.Empty((await Payload(list)).EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        }

        [Fact]
        public async Task AnIncompleteSubmit_IsRefusedFieldByField_AndMakesNoAccount()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);

            using HttpResponseMessage submit = await host.Send(
                "worker",
                HttpMethod.Post,
                $"{Route}/{id}/submit",
                Answers(1, new { firstName = "Ada", lastName = "Lovelace", email = "ada@example.com", phone = "555", dateOfBirth = "1990-01-01" }));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, submit.StatusCode);
            List<(string Field, string Keyword)> errors = Errors(await Payload(submit));
            Assert.Contains(errors, e => e.Field == "phone" && e.Keyword == ValidationKeywords.PhoneInvalidFormat);
            Assert.Contains(errors, e => e.Field == "sin" && e.Keyword == ValidationKeywords.FieldRequired);
            Assert.Empty(await host.Profiles());
            Assert.Equal(0, await host.CountEvents(id));
        }

        [Fact]
        public async Task ADateOfBirthInTheFuture_IsRefused()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);
            string tomorrow = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            using HttpResponseMessage submit = await host.Send(
                "worker",
                HttpMethod.Post,
                $"{Route}/{id}/submit",
                Answers(1, new { firstName = "Ada", lastName = "Lovelace", email = "ada@example.com", phone = "2505550199", dateOfBirth = tomorrow, sin = "050082833" }));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, submit.StatusCode);
            (string field, string keyword) = Assert.Single(Errors(await Payload(submit)));
            Assert.Equal("dateOfBirth", field);
            Assert.Equal(ValidationKeywords.RegistrationDateOfBirthInFuture, keyword);
            Assert.Empty(await host.Profiles());
        }

        [Fact]
        public async Task ASubmittedApplication_CannotBeChangedOrSubmittedAgain()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, Client)));

            using HttpResponseMessage save = await host.Send("admin", HttpMethod.Put, $"{Route}/{id}/answers", Answers(2, Client));
            using HttpResponseMessage again = await host.Send("admin", HttpMethod.Post, $"{Route}/{id}/submit", Answers(2, Client));

            Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(save));
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Single(await host.Profiles());
            Assert.Equal(1, await host.CountEvents(id));
        }

        [Fact]
        public async Task AReturnedApplication_IsResubmittedByAWorker_AndTheAccountKeepsUp()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, Client)));
            await Ok(host.Send("admin", HttpMethod.Post, $"{QueueRoute}/{id}/review", StreamVersion(1)));
            await Ok(host.Send("admin", HttpMethod.Post, $"{QueueRoute}/{id}/request-more-info", StreamVersion(2)));

            using HttpResponseMessage resubmit = await host.Send(
                "worker",
                HttpMethod.Post,
                $"{Route}/{id}/submit",
                Answers(2, new { firstName = "Ada", lastName = "King", email = "ada.king@example.com", phone = "6045550100", dateOfBirth = "1990-01-01", sin = "050082833" }));

            Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Resubmitted, (await Payload(resubmit)).GetProperty("status").GetString());
            MyssUserProfile account = Assert.Single(await host.Profiles());
            Assert.Equal("King", account.LastName);
            Assert.Equal("ada.king@example.com", account.Email);
            Assert.Equal("6045550100", Assert.Single(account.Phones).Number);
            Assert.Equal(
                [ApplicationEventTypes.Submitted, ApplicationEventTypes.PickedUp, ApplicationEventTypes.MoreInfoRequested, ApplicationEventTypes.Resubmitted],
                await host.EventTypes(id));
        }

        [Fact]
        public async Task ACitizen_NeverSeesAWorkerOverrideApplication()
        {
            using CitizenAndStaffTestHost host = NewHost(["alice"]);
            Guid id = await Start(host);

            using HttpResponseMessage mine = await host.Send("alice", HttpMethod.Get, "/v1/intake/applications");
            using HttpResponseMessage one = await host.Send("alice", HttpMethod.Get, $"/v1/intake/applications/{id}");

            Assert.Empty((await Payload(mine)).EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, one.StatusCode);
        }

        [Fact]
        public async Task OnlyAWorkerOverrideApplication_IsOpenedHere()
        {
            using CitizenAndStaffTestHost host = NewHost(["alice"]);
            Guid citizens = await host.Create("alice");

            using HttpResponseMessage read = await host.Send("worker", HttpMethod.Get, $"{Route}/{citizens}");
            using HttpResponseMessage save = await host.Send("worker", HttpMethod.Put, $"{Route}/{citizens}/answers", Answers(1, new { firstName = "Eve" }));

            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);
        }

        [Fact]
        public async Task TheDraftList_HoldsEveryWorkersDrafts_WithWhoStartedThem()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid workers = await Start(host, "worker");
            Guid admins = await Start(host, "admin");
            await Ok(host.Send(
                "worker", HttpMethod.Put, $"{Route}/{admins}/answers", Answers(1, new { firstName = " Ada ", lastName = "Lovelace" })));

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, DraftsRoute);

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            JsonElement[] drafts = [.. (await Payload(list)).EnumerateArray()];
            Assert.Equal([admins, workers], drafts.Select(d => d.GetProperty("id").GetGuid()));

            // Most recently saved first: the one just saved names its client.
            Assert.Equal("Ada Lovelace", drafts[0].GetProperty("clientName").GetString());
            Assert.Equal("MADMIN", drafts[0].GetProperty("startedBy").GetString());
            Assert.Equal(JsonValueKind.Null, drafts[1].GetProperty("clientName").ValueKind);
            Assert.Equal("MWORKER", drafts[1].GetProperty("startedBy").GetString());
            Assert.StartsWith("IA-", drafts[1].GetProperty("referenceNumber").GetString());
        }

        [Fact]
        public async Task ASubmittedApplication_LeavesTheDraftList()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid submitted = await Start(host);
            Guid draft = await Start(host);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{submitted}/submit", Answers(1, Client)));

            using HttpResponseMessage list = await host.Send("admin", HttpMethod.Get, DraftsRoute);

            JsonElement only = Assert.Single((await Payload(list)).EnumerateArray());
            Assert.Equal(draft, only.GetProperty("id").GetGuid());
        }

        [Fact]
        public async Task AReturnedApplication_StaysOutOfTheDraftList()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await Start(host);
            await Ok(host.Send("worker", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, Client)));
            await Ok(host.Send("admin", HttpMethod.Post, $"{QueueRoute}/{id}/review", StreamVersion(1)));
            await Ok(host.Send("admin", HttpMethod.Post, $"{QueueRoute}/{id}/request-more-info", StreamVersion(2)));

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, DraftsRoute);

            // It was submitted; it waits in the review queue, not here.
            Assert.Empty((await Payload(list)).EnumerateArray());
        }

        [Fact]
        public async Task ACitizensDraft_IsNotInTheDraftList()
        {
            using CitizenAndStaffTestHost host = NewHost(["alice"]);
            await host.Create("alice");

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, DraftsRoute);

            Assert.Empty((await Payload(list)).EnumerateArray());
        }

        [Theory]
        [InlineData("alice")]
        [InlineData("workernoidir")]
        public async Task OnlyAWorkerWithIdir_MayUseWorkerOverride(string persona)
        {
            using CitizenAndStaffTestHost host = NewHost(["alice"]);
            Guid id = await Start(host);

            using HttpResponseMessage start = await host.Send(persona, HttpMethod.Post, Route);
            using HttpResponseMessage drafts = await host.Send(persona, HttpMethod.Get, DraftsRoute);
            using HttpResponseMessage read = await host.Send(persona, HttpMethod.Get, $"{Route}/{id}");
            using HttpResponseMessage submit = await host.Send(persona, HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, Client));

            Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, drafts.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, submit.StatusCode);
            Assert.Single(await host.Profiles());
        }

        [Fact]
        public async Task AnAnonymousCaller_IsUnauthorized()
        {
            using CitizenAndStaffTestHost host = new(_factory, _provider, [], mockAuth: false);

            using HttpResponseMessage response = await host.Send(persona: null, HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private static JsonContent Answers(int version, object answers) => JsonContent.Create(new { version, answers });

        private static JsonContent StreamVersion(int streamVersion) => JsonContent.Create(new { streamVersion });

        private static async Task<Guid> Start(CitizenAndStaffTestHost host, string persona = "worker")
        {
            using HttpResponseMessage response = await host.Send(persona, HttpMethod.Post, Route);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await Payload(response)).GetProperty("id").GetGuid();
        }

        private static async Task Ok(Task<HttpResponseMessage> send)
        {
            using HttpResponseMessage response = await send;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private static List<(string Field, string Keyword)> Errors(JsonElement payload) =>
            payload.EnumerateArray()
                .Select(e => (e.GetProperty("field").GetString() ?? string.Empty, e.GetProperty("keyword").GetString() ?? string.Empty))
                .ToList();

        private static string[] Actions(JsonElement app) =>
            app.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString() ?? string.Empty).ToArray();

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").Clone();
        }

        private static async Task<string?> Keyword(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("keyword").GetString();
        }

        private CitizenAndStaffTestHost NewHost(IReadOnlyList<string>? registered = null) =>
            new(_factory, _provider, registered ?? [], mockAuth: true);
    }
}
