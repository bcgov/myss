namespace Myss.Api.Tests.Services
{
    using Microsoft.AspNetCore.Http;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="CorrelationIdAccessor"/>: a usable caller id is
    /// carried through, anything else falls back to the server's own.
    /// </summary>
    public class CorrelationIdAccessorTests
    {
        [Fact]
        public void UsesTheCallersHeaderWhenItLooksLikeAnId()
        {
            DefaultHttpContext context = new();
            context.Request.Headers[CorrelationIdAccessor.HeaderName] = "req-2026.09.10:0001";

            Assert.Equal("req-2026.09.10:0001", Accessor(context).CorrelationId);
        }

        [Fact]
        public void FallsBackToTheTraceIdentifierWithoutAHeader()
        {
            DefaultHttpContext context = new() { TraceIdentifier = "0HTRACE:00000001" };

            Assert.Equal("0HTRACE:00000001", Accessor(context).CorrelationId);
        }

        [Theory]
        [InlineData("has spaces in it")]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("line\nbreak")]
        [InlineData("")]
        public void RefusesAHeaderThatIsNotAnId(string supplied)
        {
            // The value ends up in log lines, rows and an outbound header, so an
            // unusable one is dropped rather than carried.
            DefaultHttpContext context = new() { TraceIdentifier = "server-id" };
            context.Request.Headers[CorrelationIdAccessor.HeaderName] = supplied;

            Assert.Equal("server-id", Accessor(context).CorrelationId);
        }

        [Fact]
        public void RefusesAnOverlongHeader()
        {
            DefaultHttpContext context = new() { TraceIdentifier = "server-id" };
            context.Request.Headers[CorrelationIdAccessor.HeaderName] = new string('a', CorrelationIdAccessor.MaxLength + 1);

            Assert.Equal("server-id", Accessor(context).CorrelationId);
        }

        [Fact]
        public void IsNullOutsideARequest()
        {
            var accessor = new CorrelationIdAccessor(new HttpContextAccessor());

            Assert.Null(accessor.CorrelationId);
        }

        private static CorrelationIdAccessor Accessor(HttpContext context) =>
            new(new HttpContextAccessor { HttpContext = context });
    }
}
