namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// The Notify email send (MYSS-36), in the request shape of Notify's
    /// published OpenAPI document. Never reaches a real Notify.
    /// </summary>
    public class NotifyEmailProviderTests
    {
        private static readonly EmailMessage Message = new("ada@example.com", "Reset your PIN", "[Reset](https://myss.test/pin-reset#token=x)");

        [Fact]
        public async Task PostsTheEmailWithTheKeyAndReturnsNotifysId()
        {
            var handler = new CapturingHandler(HttpStatusCode.Accepted, """{ "notifyId": "7c9e6679-7425-40de-944b-e07fc1f90ae7", "status": "accepted" }""");

            string id = await NewProvider(handler).SendAsync(Message, CancellationToken.None);

            Assert.Equal("7c9e6679-7425-40de-944b-e07fc1f90ae7", id);
            HttpRequestMessage request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://notify.test/api/v1/notifysimple/email", request.RequestUri!.ToString());
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("X-API-KEY")));

            using JsonDocument body = JsonDocument.Parse(handler.Bodies[0]);
            JsonElement root = body.RootElement;
            Assert.Equal("ada@example.com", Assert.Single(root.GetProperty("recipients").GetProperty("to").EnumerateArray()).GetString());
            Assert.Equal("Reset your PIN", root.GetProperty("content").GetProperty("subject").GetString());
            Assert.Equal(Message.MarkdownBody, root.GetProperty("content").GetProperty("body").GetString());
            Assert.Equal("markdown", root.GetProperty("content").GetProperty("bodyType").GetString());
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, true)]
        [InlineData(HttpStatusCode.UnprocessableEntity, true)]
        [InlineData(HttpStatusCode.Unauthorized, false)]
        [InlineData(HttpStatusCode.TooManyRequests, false)]
        [InlineData(HttpStatusCode.InternalServerError, false)]
        public async Task ARefusalSaysWhetherItWasTheAddress(HttpStatusCode status, bool recipientRejected)
        {
            var handler = new CapturingHandler(status, "{}");

            EmailNotSentException ex = await Assert.ThrowsAsync<EmailNotSentException>(
                () => NewProvider(handler).SendAsync(Message, CancellationToken.None));

            // MYSS-326: the address is the citizen's to fix; the rest is ours or Notify's.
            Assert.Equal(recipientRejected, ex.RecipientRejected);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.UnprocessableEntity)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task ARefusalIsNotSent(HttpStatusCode status)
        {
            var handler = new CapturingHandler(status, """{ "message": "ada@example.com is not on the safelist" }""");

            EmailNotSentException ex = await Assert.ThrowsAsync<EmailNotSentException>(
                () => NewProvider(handler).SendAsync(Message, CancellationToken.None));

            // Notify's text can echo the address; it must not reach the exception, which is logged.
            Assert.DoesNotContain("ada@example.com", ex.Message, StringComparison.Ordinal);
            Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnAcceptanceWithoutAnIdIsNotSent()
        {
            var handler = new CapturingHandler(HttpStatusCode.Accepted, "{}");

            await Assert.ThrowsAsync<EmailNotSentException>(
                () => NewProvider(handler).SendAsync(Message, CancellationToken.None));
        }

        [Fact]
        public async Task AnUnreachableNotifyIsNotSent()
        {
            var handler = new CapturingHandler(new HttpRequestException("connection refused"));

            await Assert.ThrowsAsync<EmailNotSentException>(
                () => NewProvider(handler).SendAsync(Message, CancellationToken.None));
        }

        [Theory]
        [InlineData(null, "test-key")]
        [InlineData("https://notify.test", null)]
        [InlineData("https://notify.test", " ")]
        [InlineData("http://notify.test", "test-key")]
        public async Task WithoutAnHttpsBaseUrlAndAKeyNothingIsCalled(string? baseAddress, string? apiKey)
        {
            var handler = new CapturingHandler(HttpStatusCode.Accepted, "{}");
            var config = new NotifyConfig { BaseUrl = baseAddress is null ? null : new Uri(baseAddress), ApiKey = apiKey };

            await Assert.ThrowsAsync<EmailNotSentException>(
                () => NewProvider(handler, config).SendAsync(Message, CancellationToken.None));
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public void TheMessageDoesNotPrintTheRecipient()
        {
            Assert.DoesNotContain("ada@example.com", Message.ToString(), StringComparison.Ordinal);
        }

        private static NotifyEmailProvider NewProvider(CapturingHandler handler, NotifyConfig? config = null) => new(
            NullLogger<NotifyEmailProvider>.Instance,
            new HttpClient(handler),
            Options.Create(config ?? new NotifyConfig { BaseUrl = new Uri("https://notify.test/"), ApiKey = "test-key" }));

        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body = string.Empty;
            private readonly Exception? _failure;

            public CapturingHandler(HttpStatusCode status, string body)
            {
                _status = status;
                _body = body;
            }

            public CapturingHandler(Exception failure)
            {
                _failure = failure;
            }

            public List<HttpRequestMessage> Requests { get; } = [];

            public List<string> Bodies { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
                if (_failure is not null)
                {
                    throw _failure;
                }

                return new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_body, Encoding.UTF8, "application/json"),
                };
            }
        }
    }
}
