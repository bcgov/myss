namespace Myss.Api.Tests.TestDoubles
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Myss.Api.Configuration;

    /// <summary>
    /// Sets the three mock-auth gate values as the last (highest-precedence)
    /// config source. <c>UseSetting</c> is not enough here: host settings are
    /// read first, so a developer's gitignored <c>appsettings.local.json</c> —
    /// which the sample tells them to create with mock auth on — would
    /// override the test and break the suite.
    /// </summary>
    public static class MockAuthSettingsExtensions
    {
        /// <summary>
        /// Pins the mock-auth gate for a test host, beating every file source.
        /// </summary>
        /// <param name="builder">The test host builder.</param>
        /// <param name="allowMockAuth">Lock 1: the build permits mock auth.</param>
        /// <param name="environmentName">Lock 2: the named environment.</param>
        /// <param name="mockAuth">Lock 3: mock auth is switched on.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IWebHostBuilder UseMockAuthSettings(
            this IWebHostBuilder builder,
            string allowMockAuth,
            string environmentName,
            string mockAuth)
        {
            return builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [MockAuthGate.AllowMockAuthKey] = allowMockAuth,
                    [MockAuthGate.EnvironmentNameKey] = environmentName,
                    [MockAuthGate.MockAuthKey] = mockAuth,
                }));
        }
    }
}
