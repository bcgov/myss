namespace Icm.Api.Host.Tests
{
    using Icm.Api.Host.Services;
    using Microsoft.AspNetCore.Http;

    public class CorrelationIdTests
    {
        [Theory]
        [InlineData("abc-123", true)]
        [InlineData("0HN7:00000001", true)]
        [InlineData("a.b_c", true)]
        [InlineData("", false)]
        [InlineData("has space", false)]
        [InlineData("new\nline", false)]
        public void OnlyIdLikeValuesAreUsable(string value, bool usable)
        {
            Assert.Equal(usable, CorrelationIdAccessor.IsUsable(value));
        }

        [Fact]
        public void TooLongIsNotUsable()
        {
            Assert.False(CorrelationIdAccessor.IsUsable(new string('a', CorrelationIdAccessor.MaxLength + 1)));
        }

        [Fact]
        public void AUsableInboundHeaderIsTheId_OtherwiseTheTraceIdentifier()
        {
            var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
            var accessor = new CorrelationIdAccessor(new HttpContextAccessor { HttpContext = context });

            Assert.Equal("trace-1", accessor.CorrelationId);

            context.Request.Headers[CorrelationIdAccessor.HeaderName] = "myss-req-42";
            Assert.Equal("myss-req-42", accessor.CorrelationId);

            context.Request.Headers[CorrelationIdAccessor.HeaderName] = "not an id";
            Assert.Equal("trace-1", accessor.CorrelationId);
        }

        [Fact]
        public async Task TheForwardingHandler_PutsTheIdOnOutboundCalls()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationIdAccessor.HeaderName] = "myss-req-42";
            var accessor = new CorrelationIdAccessor(new HttpContextAccessor { HttpContext = context });

            var recorder = new RecordingHandler();
            using var handler = new CorrelationIdForwardingHandler(accessor) { InnerHandler = recorder };
            using var client = new HttpClient(handler);

            using HttpResponseMessage _ = await client.GetAsync(new Uri("https://icm.test.invalid/gov/v1.0/x"));

            Assert.Equal("myss-req-42", recorder.Header);
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public string? Header { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Header = request.Headers.TryGetValues(CorrelationIdAccessor.HeaderName, out IEnumerable<string>? values) ? values.Single() : null;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }
        }
    }
}
