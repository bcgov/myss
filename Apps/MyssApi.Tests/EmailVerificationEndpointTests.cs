namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.Api.Controllers;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Changing and proving the email address over real HTTP (MYSS-305): the
    /// changes need the citizen's sign-in and answer with the account, following
    /// the link does not, and each refusal carries its keyword.
    /// </summary>
    public class EmailVerificationEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string ChangeEmail = "/v1/account/email";
        private const string Resend = "/v1/account/email/verification-requests";
        private const string Pending = "/v1/account/email/pending";
        private const string Verify = "/v1/email-verification";

        private readonly WebApplicationFactory<Startup> _factory;

        /// <summary>Initializes a new instance of the <see cref="EmailVerificationEndpointTests"/> class.</summary>
        /// <param name="factory">The shared host factory.</param>
        public EmailVerificationEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AnAnonymousCallerCannotChangeAnAddress()
        {
            using IntakeTestHost host = NewHost(registered: [], mockAuth: false);

            using HttpResponseMessage response = await host.Send(null, HttpMethod.Put, ChangeEmail, Body("new@example.com"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AChangeAnswersWithTheAccountShowingThePendingAddress()
        {
            var email = new FakeEmailProvider();
            using IntakeTestHost host = NewHost(registered: ["alice"], email: email);

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Put, ChangeEmail, Body("alice.new@example.com"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = await Json(response);
            JsonElement account = body.RootElement.GetProperty("payload");
            Assert.Equal("alice@example.com", account.GetProperty("email").GetString());
            Assert.Equal("alice.new@example.com", account.GetProperty("pendingEmail").GetString());
            Assert.Equal("alice.new@example.com", Assert.Single(email.Sent).To);
        }

        [Fact]
        public async Task AnInvalidAddressIs422OnTheEmailField()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Put, ChangeEmail, Body("not-an-address"));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            using JsonDocument body = await Json(response);
            JsonElement error = Assert.Single(body.RootElement.GetProperty("payload").EnumerateArray());
            Assert.Equal("email", error.GetProperty("field").GetString());
            Assert.Equal(ValidationKeywords.EmailInvalidFormat, error.GetProperty("keyword").GetString());
        }

        [Fact]
        public async Task TheLinkSwapsTheAddressWithoutASignIn_Once()
        {
            var email = new FakeEmailProvider();
            using IntakeTestHost host = NewHost(registered: ["alice"], email: email);
            using (await host.Send("alice", HttpMethod.Put, ChangeEmail, Body("alice.new@example.com")))
            {
            }

            object token = new { token = email.LastVerificationToken() };
            using HttpResponseMessage first = await host.Send(null, HttpMethod.Post, Verify, JsonContent.Create(token));
            using HttpResponseMessage second = await host.Send(null, HttpMethod.Post, Verify, JsonContent.Create(token));
            using HttpResponseMessage account = await host.Send("alice", HttpMethod.Get, "/v1/account");

            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            Assert.Equal(HttpStatusCode.Gone, second.StatusCode);
            Assert.Equal(EmailVerificationController.LinkInvalidKeyword, await Keyword(second));
            using JsonDocument body = await Json(account);
            JsonElement payload = body.RootElement.GetProperty("payload");
            Assert.Equal("alice.new@example.com", payload.GetProperty("email").GetString());
            Assert.True(payload.GetProperty("emailVerified").GetBoolean());
        }

        [Fact]
        public async Task ResendingAtOnceIs429WithRetryAfter()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            using (await host.Send("alice", HttpMethod.Put, ChangeEmail, Body("alice.new@example.com")))
            {
            }

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Post, Resend);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.NotNull(response.Headers.RetryAfter?.Delta);
            Assert.Equal(EmailVerificationController.TooSoonKeyword, await Keyword(response));
        }

        [Fact]
        public async Task ResendingWithNothingToVerifyIs409()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Post, Resend);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(EmailVerificationController.NothingToVerifyKeyword, await Keyword(response));
        }

        [Fact]
        public async Task CancellingAnswersWithTheAccountWithoutThePendingAddress()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            using (await host.Send("alice", HttpMethod.Put, ChangeEmail, Body("alice.new@example.com")))
            {
            }

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Delete, Pending);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = await Json(response);
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("payload").GetProperty("pendingEmail").ValueKind);
        }

        [Fact]
        public async Task AnAddressTheEmailServiceRefusesIs422WithItsKeyword()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"], email: new FakeEmailProvider { RejectRecipient = true });

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Put, ChangeEmail, Body("alice.new@example.com"));

            // A field error on the address, in the 422 shape the webclient reads.
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            using JsonDocument body = await Json(response);
            JsonElement error = Assert.Single(body.RootElement.GetProperty("payload").EnumerateArray());
            Assert.Equal("email", error.GetProperty("field").GetString());
            Assert.Equal(ValidationKeywords.EmailAddressRejected, error.GetProperty("keyword").GetString());
        }

        [Fact]
        public async Task AnEmailThatCannotBeSentIs503()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"], email: new FakeEmailProvider { Unavailable = true });

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Put, ChangeEmail, Body("alice.new@example.com"));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(EmailVerificationController.EmailNotSentKeyword, await Keyword(response));
        }

        private static JsonContent Body(string address) => JsonContent.Create(new { email = address });

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
