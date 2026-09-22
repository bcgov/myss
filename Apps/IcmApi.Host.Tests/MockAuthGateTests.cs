namespace Icm.Api.Host.Tests
{
    using Icm.Api.Host.Configuration;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The mock gate is a security control, so these lean on the failure cases.
    /// </summary>
    public class MockAuthGateTests
    {
        private static IConfiguration Config(params (string Key, string? Value)[] values)
        {
            var dictionary = new Dictionary<string, string?>();
            foreach ((string key, string? value) in values)
            {
                dictionary[key] = value;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(dictionary).Build();
        }

        [Fact]
        public void EnabledOnlyWhenAllThreeLocksAreOpen()
        {
            Assert.True(MockAuthGate.Evaluate(Config(
                (MockAuthGate.AllowMockAuthKey, "true"),
                (MockAuthGate.EnvironmentNameKey, "local"),
                (MockAuthGate.MockAuthKey, "true"))));
        }

        [Theory]
        [InlineData(null, "local", "true")]
        [InlineData("true", null, "true")]
        [InlineData("true", "local", null)]
        [InlineData("TRUE", "local", "yes")]
        public void DisabledWhenAnyLockIsClosed(string? allow, string? environment, string? mock)
        {
            Assert.False(MockAuthGate.Evaluate(Config(
                (MockAuthGate.AllowMockAuthKey, allow),
                (MockAuthGate.EnvironmentNameKey, environment),
                (MockAuthGate.MockAuthKey, mock))));
        }

        [Fact]
        public void DisabledWhenNothingIsConfigured()
        {
            Assert.False(MockAuthGate.Evaluate(Config()));
        }

        [Theory]
        [InlineData("prod")]
        [InlineData("Production")]
        [InlineData(" PRD ")]
        public void ProductionNamedEnvironmentWithAFlag_RefusesToStart(string environment)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => MockAuthGate.Evaluate(Config(
                (MockAuthGate.AllowMockAuthKey, "true"),
                (MockAuthGate.EnvironmentNameKey, environment))));

            Assert.Contains("Icm_AllowMockAuth", ex.Message, StringComparison.Ordinal);
        }
    }
}
