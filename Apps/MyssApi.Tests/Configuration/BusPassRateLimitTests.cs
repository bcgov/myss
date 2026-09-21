namespace Myss.Api.Tests.Configuration
{
    using Microsoft.AspNetCore.RateLimiting;
    using Myss.Api.Configuration;
    using Myss.Api.Configuration.Models;
    using Xunit;

    /// <summary>
    /// A bad limit must stop the app at startup, not answer 500 to the first citizen.
    /// </summary>
    public class BusPassRateLimitTests
    {
        [Theory]
        [InlineData(0, 60)]
        [InlineData(-1, 60)]
        [InlineData(5, 0)]
        [InlineData(5, -30)]
        public void AZeroOrNegativeValue_RefusesToStart(int permitLimit, int windowSeconds)
        {
            var config = new BusPassRateLimitConfig { PermitLimit = permitLimit, WindowSeconds = windowSeconds };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => BusPassRateLimit.Validate(config));

            Assert.Contains("BusPass:SubmitRateLimit", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheDefaults_AreUsable()
        {
            var options = new RateLimiterOptions();

            BusPassRateLimit.Configure(options, new BusPassRateLimitConfig());

            Assert.Equal(429, options.RejectionStatusCode);
        }
    }
}
