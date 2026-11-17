namespace Myss.Common.Tests.Hosting
{
    using Microsoft.Extensions.Configuration;
    using Myss.Common.Hosting;

    /// <summary>
    /// Tests for <see cref="StartupConfiguration.ReadAllowedOrigins"/>: CORS only ever
    /// allows the listed webclient origins.
    /// </summary>
    public class AllowedOriginsTests
    {
        [Fact]
        public void Unset_AllowsNoCrossOriginCalls()
        {
            Assert.Empty(StartupConfiguration.ReadAllowedOrigins(Config(null)));
        }

        [Fact]
        public void AList_IsSplitAndTrimmed()
        {
            string[] origins = StartupConfiguration.ReadAllowedOrigins(
                Config(" https://admin-dev.example.ca , http://localhost:5174,"));

            Assert.Equal(["https://admin-dev.example.ca", "http://localhost:5174"], origins);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("https://admin-dev.example.ca,*")]
        [InlineData("admin-dev.example.ca")]
        [InlineData("ftp://admin-dev.example.ca")]
        public void AnythingButAbsoluteWebOrigins_StopsTheApp(string value)
        {
            Assert.Throws<InvalidOperationException>(() => StartupConfiguration.ReadAllowedOrigins(Config(value)));
        }

        private static IConfiguration Config(string? allowOrigins)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AllowOrigins"] = allowOrigins })
                .Build();
        }
    }
}
