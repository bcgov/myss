namespace Myss.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Configuration;
    using Myss.Api.Configuration;
    using Xunit;

    /// <summary>
    /// The mock gate is a security control, so these lean on the failure cases: it must be
    /// impossible to enable by accident, and impossible at all in production.
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

        private static IConfiguration AllLocksOpen(string environmentName = "local") =>
            Config(
                (MockAuthGate.AllowMockAuthKey, "true"),
                (MockAuthGate.EnvironmentNameKey, environmentName),
                (MockAuthGate.MockAuthKey, "true"));

        [Fact]
        public void EnabledOnlyWhenAllThreeLocksAreOpen()
        {
            Assert.True(MockAuthGate.Evaluate(AllLocksOpen()));
        }

        [Fact]
        public void DisabledWhenNothingIsConfigured()
        {
            Assert.False(MockAuthGate.Evaluate(Config()));
        }

        [Fact]
        public void DisabledWhenAllowFlagIsMissing()
        {
            Assert.False(MockAuthGate.Evaluate(Config(
                (MockAuthGate.EnvironmentNameKey, "local"),
                (MockAuthGate.MockAuthKey, "true"))));
        }

        [Fact]
        public void DisabledWhenMockFlagIsMissing()
        {
            Assert.False(MockAuthGate.Evaluate(Config(
                (MockAuthGate.AllowMockAuthKey, "true"),
                (MockAuthGate.EnvironmentNameKey, "local"))));
        }

        [Fact]
        public void DisabledWhenEnvironmentNameIsMissing()
        {
            Assert.False(MockAuthGate.Evaluate(Config(
                (MockAuthGate.AllowMockAuthKey, "true"),
                (MockAuthGate.MockAuthKey, "true"))));
        }

        [Theory]
        [InlineData("false")]
        [InlineData("")]
        [InlineData("yes")]
        [InlineData("1")]
        [InlineData("TRUE ")]
        public void OnlyTheLiteralTrueOpensALock(string allowValue)
        {
            IConfiguration configuration = Config(
                (MockAuthGate.AllowMockAuthKey, allowValue),
                (MockAuthGate.EnvironmentNameKey, "local"),
                (MockAuthGate.MockAuthKey, "true"));

            Assert.False(MockAuthGate.Evaluate(configuration));
        }

        [Fact]
        public void CaseInsensitiveTrueIsAccepted()
        {
            IConfiguration configuration = Config(
                (MockAuthGate.AllowMockAuthKey, "True"),
                (MockAuthGate.EnvironmentNameKey, "local"),
                (MockAuthGate.MockAuthKey, "TRUE"));

            Assert.True(MockAuthGate.Evaluate(configuration));
        }

        [Theory]
        [InlineData("prod")]
        [InlineData("prd")]
        [InlineData("production")]
        [InlineData("PRODUCTION")]
        [InlineData("Prod")]
        public void ProductionNamedEnvironmentWithTheFlagsRefusesToStart(string environmentName)
        {
            IConfiguration configuration = AllLocksOpen(environmentName);

            var exception = Assert.Throws<InvalidOperationException>(
                () => MockAuthGate.Evaluate(configuration));

            Assert.Contains(environmentName, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProductionThrowsEvenIfOnlyOneFlagIsSet()
        {
            IConfiguration configuration = Config(
                (MockAuthGate.AllowMockAuthKey, "true"),
                (MockAuthGate.EnvironmentNameKey, "production"));

            Assert.Throws<InvalidOperationException>(() => MockAuthGate.Evaluate(configuration));
        }

        [Fact]
        public void ProductionWithoutTheFlagsIsSimplyDisabled()
        {
            IConfiguration configuration = Config((MockAuthGate.EnvironmentNameKey, "production"));

            Assert.False(MockAuthGate.Evaluate(configuration));
        }

        [Theory]
        [InlineData("local")]
        [InlineData("dev")]
        [InlineData("test")]
        [InlineData("Development")]
        public void NonProductionEnvironmentsMayEnableIt(string environmentName)
        {
            Assert.True(MockAuthGate.Evaluate(AllLocksOpen(environmentName)));
        }

        [Fact]
        public void ThrowsOnNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => MockAuthGate.Evaluate(null!));
        }
    }
}
