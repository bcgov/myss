namespace Myss.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.FeatureManagement;
    using Myss.Api.Configuration;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Common.Models;
    using Xunit;

    /// <summary>
    /// The feature flags through the real pipeline: the values the webclient
    /// reads, the 404 a gated endpoint answers when its flag is off, and a
    /// flag that is not set counting as off. The suite runs with every flag
    /// on (<see cref="TestSupport.AllFeaturesOn"/>); these tests switch them
    /// off one at a time.
    /// </summary>
    public class FeatureFlagTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> factory;

        /// <summary>Initializes a new instance of the <see cref="FeatureFlagTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public FeatureFlagTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        /// <summary>A host with the given flags overridden.</summary>
        private HttpClient CreateClient(Dictionary<string, string?> flags) =>
            this.factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseMockAuthSettings(
                        allowMockAuth: "false", environmentName: "test", mockAuth: "false");
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        Dictionary<string, string?> settings = [];
                        foreach ((string flag, string? value) in flags)
                        {
                            settings[$"{FeatureFlags.SectionName}:{flag}"] = value;
                        }

                        config.AddInMemoryCollection(settings);
                    });
                })
                .CreateClient();

        [Fact(DisplayName = "features lists every flag")]
        public async Task FeaturesListsEveryFlag()
        {
            HttpClient client = this.CreateClient([]);

            BaseResponseModel<Dictionary<string, bool>>? body =
                await client.GetFromJsonAsync<BaseResponseModel<Dictionary<string, bool>>>(
                    new Uri("/v1/features", UriKind.Relative));

            Assert.NotNull(body);
            Assert.Equal(FeatureFlags.All, body.Payload.Keys);
            Assert.All(body.Payload.Values, Assert.True);
        }

        [Fact(DisplayName = "a flag switched off is reported off")]
        public async Task SwitchedOffFlagIsReportedOff()
        {
            HttpClient client = this.CreateClient(new() { [FeatureFlags.BusPass] = "false" });

            BaseResponseModel<Dictionary<string, bool>>? body =
                await client.GetFromJsonAsync<BaseResponseModel<Dictionary<string, bool>>>(
                    new Uri("/v1/features", UriKind.Relative));

            Assert.NotNull(body);
            Assert.False(body.Payload[FeatureFlags.BusPass]);
            Assert.True(body.Payload[FeatureFlags.Intake]);
        }

        [Fact(DisplayName = "a gated endpoint answers 404 when its flag is off and works when on")]
        public async Task GatedEndpointFollowsItsFlag()
        {
            HttpClient on = this.CreateClient([]);
            HttpClient off = this.CreateClient(new() { [FeatureFlags.TechDemos] = "false" });

            HttpResponseMessage whenOn = await on.GetAsync(new Uri("/v1/Demo", UriKind.Relative));
            HttpResponseMessage whenOff = await off.GetAsync(new Uri("/v1/Demo", UriKind.Relative));

            Assert.Equal(HttpStatusCode.OK, whenOn.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, whenOff.StatusCode);
        }

        [Fact(DisplayName = "an anonymous public endpoint answers 404 when its flag is off")]
        public async Task AnonymousEndpointIsGated()
        {
            HttpClient client = this.CreateClient(new() { [FeatureFlags.EligibilityEstimator] = "false" });

            HttpResponseMessage response = await client.GetAsync(
                new Uri("/v1/EligibilityEstimator/rates", UriKind.Relative));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact(DisplayName = "a flag that is not set is off")]
        public async Task UnsetFlagIsOff()
        {
            ServiceCollection services = new();
            services.AddFeatureManagement(new ConfigurationBuilder().Build());
            await using ServiceProvider provider = services.BuildServiceProvider();

            IFeatureManager features = provider.GetRequiredService<IFeatureManager>();

            Assert.False(await features.IsEnabledAsync(FeatureFlags.BusPass));
        }
    }
}
