namespace Myss.AdminApi.Tests.Intake
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.AdminApi.Tests.TestSupport;
    using Myss.Data.Intake;
    using Myss.FormSpec;

    /// <summary>
    /// The review shows each application its form as it applied to the channel
    /// the application was started in: one published form with channel rules,
    /// reviewed once as a citizen's and once as a worker override.
    /// </summary>
    public class ReviewChannelTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/review/applications";
        private const string WorkerOverrideRoute = "/v1/intake/worker-override/applications";

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
            { "type": "email", "key": "email", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "phone", "input": true, "validate": { "required": true }, "properties": { "myssValidator": "phone" } },
            { "type": "datetime", "key": "dateOfBirth", "input": true, "validate": { "required": true }, "properties": { "myssValidator": "date" } },
            { "type": "textfield", "key": "sin", "input": true, "validate": { "required": true }, "properties": { "myssValidator": "sin", "myssChannels": { "worker-override": { "required": false } } } },
            { "type": "textfield", "key": "workerNote", "input": true, "properties": { "myssChannels": { "client": { "show": false } } } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        // Synthetic: the SIN passes Luhn and belongs to no one.
        private static readonly object ClientAnswers = new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = "ada@example.com",
            phone = "2505550199",
            dateOfBirth = "1990-01-01",
            sin = "050082833",
            submit = true,
        };

        private static readonly object WorkerAnswers = new
        {
            firstName = "Grace",
            lastName = "Hopper",
            email = "grace@example.com",
            phone = "2505550100",
            dateOfBirth = "1990-01-01",
            workerNote = "Called in.",
            submit = true,
        };

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="ReviewChannelTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public ReviewChannelTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            FormSpecModel form = FakeFormSpecProvider.Spec(IntakeFormSpecIds.ApplicationForAssistance, 1, Spec);
            _provider.LatestResult = form;
            _provider.VersionResult = form;
        }

        [Fact]
        public async Task AClientApplication_IsReviewedOnTheFormForAClient()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await host.Submit("alice", id, 1, ClientAnswers);

            JsonElement spec = await ReviewedSpec(host, id);

            Assert.False(HasComponent(spec, "workerNote"));
            Assert.True(Required(spec, "sin"));
            Assert.DoesNotContain("myssChannels", spec.GetRawText(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task AWorkerOverride_IsReviewedOnTheFormForAWorker()
        {
            using CitizenAndStaffTestHost host = NewHost();
            using HttpResponseMessage start = await host.Send("worker", HttpMethod.Post, WorkerOverrideRoute);
            Guid id = (await Payload(start)).GetProperty("id").GetGuid();
            using HttpResponseMessage submit = await host.Send(
                "worker",
                HttpMethod.Post,
                $"{WorkerOverrideRoute}/{id}/submit",
                JsonContent.Create(new { version = 1, answers = WorkerAnswers }));
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

            JsonElement spec = await ReviewedSpec(host, id);

            Assert.True(HasComponent(spec, "workerNote"));
            Assert.False(Required(spec, "sin"));
            Assert.DoesNotContain("myssChannels", spec.GetRawText(), StringComparison.Ordinal);
        }

        private static async Task<JsonElement> ReviewedSpec(CitizenAndStaffTestHost host, Guid id)
        {
            using HttpResponseMessage read = await host.Send("worker", HttpMethod.Get, $"{Route}/{id}");
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            return (await Payload(read)).GetProperty("spec").GetProperty("spec");
        }

        private static bool HasComponent(JsonElement spec, string key) =>
            spec.GetProperty("components").EnumerateArray().Any(c => c.GetProperty("key").GetString() == key);

        private static bool Required(JsonElement spec, string key) =>
            spec.GetProperty("components").EnumerateArray()
                .Single(c => c.GetProperty("key").GetString() == key)
                .GetProperty("validate").GetProperty("required").GetBoolean();

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").Clone();
        }

        private CitizenAndStaffTestHost NewHost() => new(_factory, _provider, ["alice"], mockAuth: true);
    }
}
