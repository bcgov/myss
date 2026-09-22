namespace Icm.Api.Host.Configuration
{
    using System;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The three-lock gate controlling local mock authentication, the same
    /// control MyssApi has.
    /// <para>
    /// Mock auth lets a developer call this host without a token from the shared
    /// realm. It is <b>fail-closed</b>: every lock must be explicitly opened, an
    /// unset value always means disabled, and a production-named environment that
    /// sees the flags at all is treated as a deployment accident and stops the app.
    /// </para>
    /// <para>
    /// Keys come from environment variables prefixed <c>Icm_</c> (so
    /// <c>Icm_AllowMockAuth</c> binds to <c>AllowMockAuth</c>), matching
    /// <see cref="ProgramConfiguration"/>.
    /// </para>
    /// </summary>
    public static class MockAuthGate
    {
        /// <summary>Lock 1: the build/deployment permits mock auth at all.</summary>
        public const string AllowMockAuthKey = "AllowMockAuth";

        /// <summary>Lock 2: the environment must be named, and must not be production.</summary>
        public const string EnvironmentNameKey = "EnvironmentName";

        /// <summary>Lock 3: mock auth is actually switched on.</summary>
        public const string MockAuthKey = "MockAuth";

        private static readonly string[] ProductionNames = ["prod", "prd", "production"];

        /// <summary>
        /// Decides whether mock authentication should be enabled.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns><c>true</c> only when all three locks are open.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a production-named environment has either mock flag set.
        /// </exception>
        public static bool Evaluate(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            string? allowMockAuth = configuration[AllowMockAuthKey];
            string? mockAuth = configuration[MockAuthKey];
            string? environmentName = configuration[EnvironmentNameKey];

            bool anyFlagRequested = IsTrue(allowMockAuth) || IsTrue(mockAuth);

            if (anyFlagRequested && IsProductionName(environmentName))
            {
                throw new InvalidOperationException(
                    $"Mock authentication was requested in environment '{environmentName}'. "
                    + "Mock authentication must never be enabled in production. "
                    + $"Unset {AllowMockAuthKey}/{MockAuthKey} (env: {ProgramConfiguration.EnvironmentPrefix}{AllowMockAuthKey}, "
                    + $"{ProgramConfiguration.EnvironmentPrefix}{MockAuthKey}).");
            }

            if (!IsTrue(allowMockAuth) || !IsTrue(mockAuth))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(environmentName))
            {
                return false;
            }

            return !IsProductionName(environmentName);
        }

        private static bool IsTrue(string? value) =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        private static bool IsProductionName(string? environmentName)
        {
            if (string.IsNullOrWhiteSpace(environmentName))
            {
                return false;
            }

            string trimmed = environmentName.Trim();
            return Array.Exists(
                ProductionNames,
                name => string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase));
        }
    }
}
