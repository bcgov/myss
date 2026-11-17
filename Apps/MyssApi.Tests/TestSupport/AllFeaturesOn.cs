namespace Myss.Api.Tests.TestSupport
{
    using System;
    using System.Runtime.CompilerServices;
    using Myss.Api.Configuration;

    /// <summary>
    /// Every feature flag is off unless set, which would turn every endpoint
    /// test into a 404. Each test host reads the <c>Myss_</c> environment
    /// variables, so switching every flag on here covers the whole suite;
    /// <see cref="FeatureFlagTests"/> switches single flags off on top.
    /// </summary>
    internal static class AllFeaturesOn
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            foreach (string flag in FeatureFlags.All)
            {
                Environment.SetEnvironmentVariable($"Myss_{FeatureFlags.SectionName}__{flag}", "true");
            }
        }
    }
}
