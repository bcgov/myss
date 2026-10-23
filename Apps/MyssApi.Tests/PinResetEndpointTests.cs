namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.Api.Controllers;
    using Myss.Api.Domain;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Resetting a forgotten PIN over real HTTP (MYSS-36): asking needs the
    /// citizen's sign-in, using the emailed link does not, and each refusal
    /// carries its keyword.
    /// </summary>
    public class PinResetEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Requests = "/v1/pin-reset/requests";
        private const string Reset = "/v1/pin-reset";

        private readonly WebApplicationFactory<Startup> _factory;

        /// <summary>Initializes a new instance of the <see cref="PinResetEndpointTests"/> class.</summary>
        /// <param name="factory">The shared host factory.</param>
        public PinResetEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AnAnonymousCallerCannotAskForALink()
        {
            using IntakeTestHost host = NewHost(registered: [], mockAuth: false);

            using HttpResponseMessage response = await host.Send(null, HttpMethod.Post, Requests);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AWorkerCannotAskForALink()
        {
            using IntakeTestHost host = NewHost(registered: ["worker"]);

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Post, Requests);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task ABcscCitizenHasNoPinToReset()
        {
            using IntakeTestHost host = NewHost(registered: ["bcsc"]);

            using HttpResponseMessage response = await host.Send("bcsc", HttpMethod.Post, Requests);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(ValidationKeywords.PinNotAvailable, await Keyword(response));
        }

        [Fact]
        public async Task AskingSendsTheEmailAndSaysWhenTheLinkExpires()
        {
            var email = new FakeEmailProvider();
            using IntakeTestHost host = NewHost(registered: ["alice"], email: email);

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Post, Requests);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = await Json(response);
            Assert.True(body.RootElement.GetProperty("payload").TryGetProperty("expiresAt", out _));
            Assert.Equal("alice@example.com", Assert.Single(email.Sent).To);
        }

        [Fact]
        public async Task AskingAgainAtOnceIs429WithRetryAfter()
        {
            var email = new FakeEmailProvider();
            using IntakeTestHost host = NewHost(registered: ["alice"], email: email);
            using (await host.Send("alice", HttpMethod.Post, Requests))
            {
            }

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Post, Requests);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.NotNull(response.Headers.RetryAfter?.Delta);
            Assert.Equal(PinResetController.TooSoonKeyword, await Keyword(response));
            Assert.Single(email.Sent);
        }

        [Fact]
        public async Task AnEmailThatCannotBeSentIs503()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"], email: new FakeEmailProvider { Unavailable = true });

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Post, Requests);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(PinResetController.EmailNotSentKeyword, await Keyword(response));
        }

        [Fact]
        public async Task TheLinkResetsThePinWithoutASignIn_Once()
        {
            var email = new FakeEmailProvider();
            using IntakeTestHost host = NewHost(registered: ["alice"], email: email);
            using (await host.Send("alice", HttpMethod.Post, Requests))
            {
            }

            object body = new { token = email.LastResetToken(), newPin = "7350", confirmPin = "7350" };
            using HttpResponseMessage first = await host.Send(null, HttpMethod.Post, Reset, JsonContent.Create(body));
            using HttpResponseMessage second = await host.Send(null, HttpMethod.Post, Reset, JsonContent.Create(body));

            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            Assert.Equal(HttpStatusCode.Gone, second.StatusCode);
            Assert.Equal(PinResetController.LinkInvalidKeyword, await Keyword(second));
        }

        [Fact]
        public async Task AMistypedPinIs422WithTheField()
        {
            var email = new FakeEmailProvider();
            using IntakeTestHost host = NewHost(registered: ["alice"], email: email);
            using (await host.Send("alice", HttpMethod.Post, Requests))
            {
            }

            using HttpResponseMessage response = await host.Send(
                null,
                HttpMethod.Post,
                Reset,
                JsonContent.Create(new { token = email.LastResetToken(), newPin = "7350", confirmPin = "0000" }));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            using JsonDocument body = await Json(response);
            JsonElement error = Assert.Single(body.RootElement.GetProperty("payload").EnumerateArray());
            Assert.Equal("confirmPin", error.GetProperty("field").GetString());
            Assert.Equal(ValidationKeywords.PinMismatch, error.GetProperty("keyword").GetString());
        }

        private static async Task<JsonDocument> Json(HttpResponseMessage response) =>
            JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        private static async Task<string?> Keyword(HttpResponseMessage response)
        {
            using JsonDocument body = await Json(response);
            return body.RootElement.GetProperty("keyword").GetString();
        }

        private IntakeTestHost NewHost(IReadOnlyList<string> registered, bool mockAuth = true, FakeEmailProvider? email = null)
        {
            return new IntakeTestHost(_factory, new FakeFormSpecProvider(), registered, mockAuth, email: email);
        }
    }
}
