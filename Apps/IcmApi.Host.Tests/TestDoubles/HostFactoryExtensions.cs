namespace Icm.Api.Host.Tests.TestDoubles
{
    using Icm.Api.Host.Configuration;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;

    public static class HostFactoryExtensions
    {
        /// <summary>
        /// The non-secret ICM settings the host needs to boot. The values are
        /// placeholders: tests replace the submitter and never connect.
        /// </summary>
        public static IWebHostBuilder UseIcmSettings(this IWebHostBuilder builder, bool withCredentials = true)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Icm:BaseUrl"] = "https://icm.test.invalid/gov/v1.0",
                ["Icm:Auth:BaseUrl"] = "https://sso.test.invalid/auth",
                ["Icm:Auth:Realm"] = "icm",
            };

            if (withCredentials)
            {
                settings["Icm:Auth:ClientId"] = "icm-client";
                settings["Icm:Auth:ClientSecret"] = "not-a-real-secret";
            }

            return builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
        }

        /// <summary>
        /// Removes the ICM base URL whatever lower-priority source supplied it. An
        /// in-memory override cannot delete a key, only blank it, and the binder reads a
        /// blank <see cref="Uri"/> as unset — which is what "not configured" is.
        /// </summary>
        public static IWebHostBuilder UseNoIcmBaseUrl(this IWebHostBuilder builder)
        {
            return builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Icm:BaseUrl"] = string.Empty }));
        }

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

        /// <summary>Real bearer authentication against an authority that is never contacted.</summary>
        public static IWebHostBuilder UseRealAuthSettings(this IWebHostBuilder builder, params string[] allowedClients)
        {
            return builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    [MockAuthGate.AllowMockAuthKey] = "false",
                    [MockAuthGate.MockAuthKey] = "false",
                    ["Oidc:Authority"] = "https://sso.test.invalid/auth/realms/standard",
                };

                // appsettings.json ships a default entry; an in-memory override cannot
                // remove it, only blank it, which the host must treat as absent.
                settings["Oidc:AllowedClients:0"] = string.Empty;
                for (int i = 0; i < allowedClients.Length; i++)
                {
                    settings[$"Oidc:AllowedClients:{i}"] = allowedClients[i];
                }

                config.AddInMemoryCollection(settings);
            });
        }
    }
}
