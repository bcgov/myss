namespace Myss.AdminApi.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.AdminApi.Domain;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.Common.Auth;

    /// <summary>
    /// <c>GET</c> and <c>POST /v1/forms/{id}/acknowledgement</c> through the real pipeline
    /// (routing, authentication, the admin policy, model binding and validation), with the
    /// content engine faked.
    /// </summary>
    public class SubmissionAcknowledgementEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Path = "/v1/forms/bc-bus-pass/acknowledgement";

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeSubmissionAcknowledgementAdminProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="SubmissionAcknowledgementEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public SubmissionAcknowledgementEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task NoToken_Is401_OnBothMethods()
        {
            HttpClient client = Client(mockAuth: false);

            using HttpResponseMessage read = await client.SendAsync(Get(persona: null));
            using HttpResponseMessage save = await client.SendAsync(Post(Valid(basedOn: null), persona: null));

            Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, save.StatusCode);
            Assert.Empty(_provider.Creates);
        }

        [Theory]
        [InlineData("alice")]
        [InlineData("bceid")]
        [InlineData("workernoidir")]
        public async Task ACallerWithoutAnIdirIdentity_Is403_OnBothMethods(string persona)
        {
            HttpClient client = Client();

            using HttpResponseMessage read = await client.SendAsync(Get(persona));
            using HttpResponseMessage save = await client.SendAsync(Post(Valid(basedOn: null), persona));

            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
            Assert.Empty(_provider.Creates);
        }

        [Theory]
        [InlineData("admin", "MADMIN")]
        [InlineData("worker", "MWORKER")]
        public async Task AnIdirUser_SavesTheMessage_AndIsRecordedAsItsAuthor(string persona, string idirUsername)
        {
            using HttpResponseMessage response = await Client().SendAsync(Post(Valid(basedOn: null), persona));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement current = (await Payload(response)).GetProperty("current");
            Assert.Equal(idirUsername, current.GetProperty("changedBy").GetString());
            Assert.Equal("Bus Pass Request is complete", current.GetProperty("heading").GetString());
            Assert.Equal(idirUsername, Assert.Single(_provider.Creates).ChangedBy);
        }

        [Fact]
        public async Task AnIdirUser_ReadsTheLiveMessageAndTheHistory()
        {
            _provider.Seed();
            _provider.Seed("JSMITH");

            using HttpResponseMessage response = await Client().SendAsync(Get("worker"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement payload = await Payload(response);
            Assert.Equal("doc-2", payload.GetProperty("current").GetProperty("id").GetString());
            Assert.Equal(
                ["doc-2", "doc-1"],
                payload.GetProperty("history").EnumerateArray().Select(entry => entry.GetProperty("id").GetString()));
        }

        [Fact]
        public async Task ARefusedSave_Is422_NamingEachField()
        {
            var request = new { basedOn = (string?)null, heading = " ", title = "Request submitted", body = "No placeholder" };

            using HttpResponseMessage response = await Client().SendAsync(Post(request, "admin"));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(
                [
                    ("heading", SubmissionAcknowledgementKeywords.TextRequired),
                    ("body", SubmissionAcknowledgementKeywords.ReferenceMissing),
                ],
                (await Payload(response)).EnumerateArray().Select(error =>
                    (error.GetProperty("field").GetString(), error.GetProperty("keyword").GetString())));
            Assert.Empty(_provider.Creates);
        }

        [Fact]
        public async Task AStaleBasedOn_Is409_WithTheChangedKeyword()
        {
            _provider.Seed();
            _provider.Seed("JSMITH");

            using HttpResponseMessage response = await Client().SendAsync(Post(Valid(basedOn: "doc-1"), "admin"));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            JsonElement error = Assert.Single((await Payload(response)).EnumerateArray());
            Assert.Equal(SubmissionAcknowledgementKeywords.Changed, error.GetProperty("keyword").GetString());
            Assert.Empty(_provider.Creates);
        }

        [Fact]
        public async Task AFailedWrite_Is502()
        {
            _provider.CreateException = new ContentEngineUnavailableException("The content engine did not respond in time.");

            using HttpResponseMessage response = await Client().SendAsync(Post(Valid(basedOn: null), "admin"));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }

        [Fact]
        public async Task AFailedRead_Is502()
        {
            _provider.ReadException = new ContentEngineUnavailableException("Could not read.");

            using HttpResponseMessage response = await Client().SendAsync(Get("admin"));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }

        [Fact]
        public async Task AFormWhosePageShowsNoMessage_Is404_OnBothMethods()
        {
            const string otherPath = "/v1/forms/registration/acknowledgement";
            HttpClient client = Client();

            using HttpResponseMessage read = await client.SendAsync(Get("admin", otherPath));
            using HttpResponseMessage save = await client.SendAsync(Post(Valid(basedOn: null), "admin", otherPath));

            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);
            Assert.Empty(_provider.Creates);
        }

        private static object Valid(string? basedOn) => new
        {
            basedOn,
            heading = "Bus Pass Request is complete",
            title = "Request submitted",
            body = "Your reference number is {referenceNumber}.",
        };

        private static HttpRequestMessage Get(string? persona, string path = Path)
        {
            HttpRequestMessage request = new(HttpMethod.Get, path);
            if (persona is not null)
            {
                request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            }

            return request;
        }

        private static HttpRequestMessage Post(object body, string? persona, string path = Path)
        {
            HttpRequestMessage request = new(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            if (persona is not null)
            {
                request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            }

            return request;
        }

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").Clone();
        }

        private HttpClient Client(bool mockAuth = true)
        {
            string enabled = mockAuth ? "true" : "false";
            return _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseMockAuthSettings(allowMockAuth: enabled, environmentName: "test", mockAuth: enabled);
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<ISubmissionAcknowledgementAdminProvider>();
                        services.AddSingleton<ISubmissionAcknowledgementAdminProvider>(_provider);
                    });
                })
                .CreateClient();
        }
    }
}
