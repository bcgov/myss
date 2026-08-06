namespace Myss.Api.Configuration
{
    using System;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The three-lock gate controlling local mock authentication.
    /// <para>
    /// Mock auth exists so the whole app can be exercised before IDIM confirms the real
    /// Keycloak details. It is <b>fail-closed</b>: every lock must be explicitly opened, an
    /// unset value always means disabled, and a production-named environment that sees the
    /// flags at all is treated as a deployment accident and stops the app.
    /// </para>
    /// <para>
    /// Keys come from environment variables prefixed <c>Myss_</c> (so <c>Myss_AllowMockAuth</c>
    /// binds to <c>AllowMockAuth</c>), matching <see cref="ProgramConfiguration"/>.
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

        /// <summary>Configuration key selecting which dev persona to sign in as.</summary>
        public const string PersonaKey = "MockAuthPersona";

        private static readonly string[] ProductionNames = ["prod", "prd", "production"];

        /// <summary>
        /// Decides whether mock authentication should be enabled.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns><c>true</c> only when all three locks are open.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a production-named environment has either mock flag set — a
        /// misconfiguration that must never start rather than silently run with fake identities.
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
                    + $"Unset {AllowMockAuthKey}/{MockAuthKey} (env: Myss_{AllowMockAuthKey}, Myss_{MockAuthKey}).");
            }

            // Lock 1 and lock 3: both flags must be explicitly true.
            if (!IsTrue(allowMockAuth) || !IsTrue(mockAuth))
            {
                return false;
            }

            // Lock 2: the environment must be explicitly named (unset => disabled).
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
