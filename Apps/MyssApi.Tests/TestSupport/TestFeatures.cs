namespace Myss.Api.Tests.TestSupport
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.FeatureManagement;
    using Myss.Api.Configuration;

    /// <summary>
    /// A real feature manager over in-memory settings, for tests that build a
    /// service by hand rather than through a test host.
    /// </summary>
    public static class TestFeatures
    {
        /// <summary>Every flag on, as <see cref="AllFeaturesOn"/> sets for the hosts.</summary>
        /// <returns>The feature manager.</returns>
        public static IFeatureManager AllOn() => With(FeatureFlags.All.ToDictionary(flag => flag, _ => true));

        /// <summary>Every flag on except those named.</summary>
        /// <param name="off">The flags to switch off.</param>
        /// <returns>The feature manager.</returns>
        public static IFeatureManager AllOnExcept(params string[] off) =>
            With(FeatureFlags.All.ToDictionary(flag => flag, flag => !off.Contains(flag)));

        private static IFeatureManager With(Dictionary<string, bool> flags)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(flags.ToDictionary(
                    pair => $"{FeatureFlags.SectionName}:{pair.Key}",
                    pair => (string?)(pair.Value ? "true" : "false")))
                .Build();

            return new ServiceCollection()
                .AddFeatureManagement(configuration.GetSection(FeatureFlags.SectionName))
                .Services
                .BuildServiceProvider()
                .GetRequiredService<IFeatureManager>();
        }
    }
}
