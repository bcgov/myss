namespace Myss.Api.Tests.Configuration
{
    using Microsoft.Extensions.Configuration;
    using Myss.Api.Configuration.Models;
    using Xunit;

    /// <summary>
    /// The service credentials live under Oidc:ServiceAccount and share the realm
    /// with the rest of the Oidc section; the old IcmApi:Auth keys still work for
    /// one release so a deployment can move without a flag day.
    /// </summary>
    public class OidcServiceAccountConfigTests
    {
        [Fact]
        public void TheTokenEndpoint_DerivesFromTheAuthority()
        {
            OidcServiceAccountConfig config = OidcServiceAccountConfig.Bind(Build(new()
            {
                ["Oidc:Authority"] = "https://sso.test/realms/standard/",
                ["Oidc:ServiceAccount:ClientId"] = "myss-api",
                ["Oidc:ServiceAccount:ClientSecret"] = "s3cret",
            }));

            Assert.Equal("https://sso.test/realms/standard/protocol/openid-connect/token", config.TokenEndpoint);
            Assert.True(config.IsConfigured);
            Assert.False(config.LegacyKeysUsed);
        }

        [Fact]
        public void AnExplicitTokenEndpoint_Wins()
        {
            OidcServiceAccountConfig config = OidcServiceAccountConfig.Bind(Build(new()
            {
                ["Oidc:Authority"] = "https://sso.test/realms/standard",
                ["Oidc:ServiceAccount:TokenEndpoint"] = "https://other.test/token",
                ["Oidc:ServiceAccount:ClientId"] = "myss-api",
                ["Oidc:ServiceAccount:ClientSecret"] = "s3cret",
            }));

            Assert.Equal("https://other.test/token", config.TokenEndpoint);
        }

        [Fact]
        public void TheLegacySection_IsStillRead()
        {
            OidcServiceAccountConfig config = OidcServiceAccountConfig.Bind(Build(new()
            {
                ["Oidc:Authority"] = "https://sso.test/realms/standard",
                ["IcmApi:Auth:TokenEndpoint"] = "https://legacy.test/token",
                ["IcmApi:Auth:ClientId"] = "legacy-client",
                ["IcmApi:Auth:ClientSecret"] = "legacy-secret",
                ["IcmApi:Auth:Scope"] = "data",
            }));

            Assert.True(config.LegacyKeysUsed);
            Assert.Equal("legacy-client", config.ClientId);
            Assert.Equal("legacy-secret", config.ClientSecret);
            Assert.Equal("data", config.Scope);
            Assert.Equal("https://legacy.test/token", config.TokenEndpoint);
            Assert.True(config.IsConfigured);
        }

        [Fact]
        public void TheNewSection_TakesPrecedenceOverTheLegacyOne()
        {
            OidcServiceAccountConfig config = OidcServiceAccountConfig.Bind(Build(new()
            {
                ["Oidc:Authority"] = "https://sso.test/realms/standard",
                ["Oidc:ServiceAccount:ClientId"] = "myss-api",
                ["Oidc:ServiceAccount:ClientSecret"] = "s3cret",
                ["IcmApi:Auth:ClientId"] = "legacy-client",
                ["IcmApi:Auth:ClientSecret"] = "legacy-secret",
                ["IcmApi:Auth:TokenEndpoint"] = "https://legacy.test/token",
            }));

            Assert.False(config.LegacyKeysUsed);
            Assert.Equal("myss-api", config.ClientId);
            Assert.Equal("https://sso.test/realms/standard/protocol/openid-connect/token", config.TokenEndpoint);
        }

        [Fact]
        public void WithoutCredentials_IsNotConfigured()
        {
            OidcServiceAccountConfig config = OidcServiceAccountConfig.Bind(Build(new()
            {
                ["Oidc:Authority"] = "https://sso.test/realms/standard",
            }));

            Assert.False(config.IsConfigured);
            Assert.False(config.LegacyKeysUsed);
            Assert.NotNull(config.TokenEndpoint);
        }

        private static IConfiguration Build(Dictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
