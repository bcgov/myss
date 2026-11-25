namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.Api.Configuration;
    using Myss.Api.Intake;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// One form with channel rules, served and checked through the applicant
    /// routes for applications of each channel. Rows of the other channel are
    /// written straight to the database, since the applicant routes only start
    /// client applications.
    /// </summary>
    public class IntakeChannelTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/applications";
        private const int PinnedVersion = 2;

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "sin", "input": true, "validate": { "required": true },
              "properties": { "myssChannels": { "worker-override": { "required": false } } } },
            { "type": "textfield", "key": "workerNote", "input": true,
              "properties": { "myssChannels": { "client": { "show": false } } } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="IntakeChannelTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public IntakeChannelTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, PinnedVersion, Spec);
            _provider.VersionResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, PinnedVersion, Spec);
        }

        [Fact]
        public async Task ANewCitizenApplication_IsStoredAndServedAsAClientOne()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");

            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");

            Assert.Equal(RequestChannels.Client, await host.RowChannel(id));
            Assert.Equal(RequestChannels.Client, (await Payload(get)).GetProperty("channel").GetString());
        }

        [Theory]
        [InlineData(RequestChannels.Client, true, false)]
        [InlineData(RequestChannels.WorkerOverride, false, true)]
        public async Task TheFormServed_IsTheOneForTheApplicationsChannel(string channel, bool sinRequired, bool workerNoteShown)
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.InsertApplication("alice", channel, PinnedVersion);

            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            JsonElement spec = (await Payload(get)).GetProperty("spec").GetProperty("spec");
            Assert.Equal(sinRequired, Component(spec, "sin").GetProperty("validate").GetProperty("required").GetBoolean());
            Assert.Equal(workerNoteShown, HasComponent(spec, "workerNote"));
            Assert.DoesNotContain("myssChannels", spec.GetRawText(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task ADraftSave_RefusesAnAnswerToAFieldHiddenInTheApplicationsChannel()
        {
            using IntakeTestHost host = NewHost();
            Guid client = await host.InsertApplication("alice", RequestChannels.Client, PinnedVersion);
            Guid worker = await host.InsertApplication("alice", RequestChannels.WorkerOverride, PinnedVersion);
            var answers = new { firstName = "Ada", workerNote = "Called in." };

            using HttpResponseMessage clientSave = await host.Send("alice", HttpMethod.Put, $"{Route}/{client}/answers", Answers(1, answers));
            using HttpResponseMessage workerSave = await host.Send("alice", HttpMethod.Put, $"{Route}/{worker}/answers", Answers(1, answers));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, clientSave.StatusCode);
            Assert.Equal(ValidationKeywords.FieldUnknown, await FirstKeyword(clientSave, "workerNote"));
            Assert.Equal("{}", (await host.RowAnswers(client)).GetRawText());
            Assert.Equal(HttpStatusCode.OK, workerSave.StatusCode);
            Assert.Equal("Called in.", (await host.RowAnswers(worker)).GetProperty("workerNote").GetString());
        }

        [Fact]
        public async Task ASubmit_RefusesAnAnswerToAFieldHiddenInTheApplicationsChannel()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.InsertApplication("alice", RequestChannels.Client, PinnedVersion);

            using HttpResponseMessage submit = await host.Send(
                "alice",
                HttpMethod.Post,
                $"{Route}/{id}/submit",
                Answers(1, new { firstName = "Ada", sin = "046454286", workerNote = "Called in." }));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, submit.StatusCode);
            Assert.Equal(ValidationKeywords.FieldUnknown, await FirstKeyword(submit, "workerNote"));
            Assert.Equal(0, await host.CountEvents(id));
        }

        [Fact]
        public async Task ASubmitWithoutTheSin_FailsForAClientApplication_AndPassesForAWorkerOverrideOne()
        {
            using IntakeTestHost host = NewHost();
            Guid client = await host.InsertApplication("alice", RequestChannels.Client, PinnedVersion);
            Guid worker = await host.InsertApplication("alice", RequestChannels.WorkerOverride, PinnedVersion);
            var answers = new { firstName = "Ada" };

            using HttpResponseMessage clientSubmit = await host.Send("alice", HttpMethod.Post, $"{Route}/{client}/submit", Answers(1, answers));
            using HttpResponseMessage workerSubmit = await host.Send("alice", HttpMethod.Post, $"{Route}/{worker}/submit", Answers(1, answers));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, clientSubmit.StatusCode);
            Assert.Equal(ValidationKeywords.FieldRequired, await FirstKeyword(clientSubmit, "sin"));
            Assert.Equal(0, await host.CountEvents(client));
            Assert.Equal(HttpStatusCode.OK, workerSubmit.StatusCode);
            Assert.Equal(1, await host.CountEvents(worker));
        }

        [Fact]
        public async Task ADraftOpenedAfterANewerVersion_IsServedItsPinnedVersion_ResolvedForItsChannel()
        {
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, PinnedVersion + 1, Spec);
            using IntakeTestHost host = NewHost();
            Guid id = await host.InsertApplication("alice", RequestChannels.WorkerOverride, PinnedVersion);

            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");

            JsonElement served = (await Payload(get)).GetProperty("spec");
            Assert.Equal(PinnedVersion, served.GetProperty("version").GetInt32());
            Assert.False(Component(served.GetProperty("spec"), "sin").GetProperty("validate").GetProperty("required").GetBoolean());
            Assert.Equal((IntakeService.FormSpecId, PinnedVersion), Assert.Single(_provider.VersionCalls));
            Assert.Empty(_provider.LatestCalls);
        }

        private static JsonElement Component(JsonElement spec, string key)
        {
            return spec.GetProperty("components").EnumerateArray().Single(c => c.GetProperty("key").GetString() == key);
        }

        private static bool HasComponent(JsonElement spec, string key)
        {
            return spec.GetProperty("components").EnumerateArray().Any(c => c.GetProperty("key").GetString() == key);
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

        private IntakeTestHost NewHost()
        {
            return new IntakeTestHost(_factory, _provider, registered: ["alice"], mockAuth: true);
        }
    }
}
