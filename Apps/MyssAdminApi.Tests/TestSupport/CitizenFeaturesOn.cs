extern alias citizen;

namespace Myss.AdminApi.Tests.TestSupport
{
    using System;
    using System.Runtime.CompilerServices;
    using CitizenFeatureFlags = citizen::Myss.Api.Configuration.FeatureFlags;

    /// <summary>
    /// The citizen API's feature flags are off unless set, so every call the
    /// review tests make through it (creating and submitting an application)
    /// would answer 404. Its hosts read the <c>Myss_</c> environment variables,
    /// so switching every flag on here covers the suite, as MyssApi.Tests'
    /// AllFeaturesOn does for its own.
    /// </summary>
    internal static class CitizenFeaturesOn
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            foreach (string flag in CitizenFeatureFlags.All)
            {
                Environment.SetEnvironmentVariable($"Myss_{CitizenFeatureFlags.SectionName}__{flag}", "true");
            }
        }
    }
}
