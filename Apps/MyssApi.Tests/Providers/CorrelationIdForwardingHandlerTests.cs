namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Net.Http;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// Tests for <see cref="CorrelationIdForwardingHandler"/>.
    /// </summary>
    public class CorrelationIdForwardingHandlerTests
    {
        [Fact]
        public async Task ForwardsTheCurrentRequestsIdAsXRequestId()
        {
            RecordingHandler inner = new();
            using HttpClient client = Client(new FakeCorrelationIdAccessor("req-42"), inner);

            using HttpResponseMessage response = await client.GetAsync(new Uri("http://icm-api.test/v1/ping"));

            Assert.Equal(["req-42"], inner.LastRequest!.Headers.GetValues(CorrelationIdAccessor.HeaderName));
        }

        [Fact]
        public async Task LeavesAnIdTheCallerAlreadySetAlone()
        {
            RecordingHandler inner = new();
            using HttpClient client = Client(new FakeCorrelationIdAccessor("req-42"), inner);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://icm-api.test/v1/ping"));
            request.Headers.Add(CorrelationIdAccessor.HeaderName, "explicit-id");

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(["explicit-id"], inner.LastRequest!.Headers.GetValues(CorrelationIdAccessor.HeaderName));
        }

        [Fact]
        public async Task SendsNoHeaderOutsideARequest()
        {
            RecordingHandler inner = new();
            using HttpClient client = Client(new FakeCorrelationIdAccessor(null), inner);

            using HttpResponseMessage response = await client.GetAsync(new Uri("http://icm-api.test/v1/ping"));

            Assert.False(inner.LastRequest!.Headers.Contains(CorrelationIdAccessor.HeaderName));
        }

        private static HttpClient Client(ICorrelationIdAccessor accessor, HttpMessageHandler inner) =>
            new(new CorrelationIdForwardingHandler(accessor) { InnerHandler = inner });

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }
    }
}
