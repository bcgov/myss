namespace Myss.Api.Tests
{
    using System.Net;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Data;

    /// <summary>
    /// The confirmation message endpoint over real HTTP: anonymous, the three texts
    /// and nothing more, and a status for each other outcome.
    /// </summary>
    public class SubmissionAcknowledgementEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private static readonly Uri BusPassUrl = new("/v1/forms/bc-bus-pass/acknowledgement", UriKind.Relative);

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="SubmissionAcknowledgementEndpointTests"/> class.</summary>
        /// <param name="factory">The shared host factory.</param>
        public SubmissionAcknowledgementEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AnAnonymousCaller_GetsTheThreeTexts_AndNothingElse()
        {
            _provider.Result = SubmissionAcknowledgementLookup.Found(new SubmissionAcknowledgementModel
            {
                Heading = "Request complete",
                Title = "Submitted",
                Body = "Your reference number is {referenceNumber}.",
            });

            using HttpResponseMessage response = await CreateClient().GetAsync(BusPassUrl);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement payload = body.RootElement.GetProperty("payload");
            Assert.Equal(
                ["body", "heading", "title"],
                payload.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            Assert.Equal("Request complete", payload.GetProperty("heading").GetString());
            Assert.Equal("Submitted", payload.GetProperty("title").GetString());
            Assert.Equal("Your reference number is {referenceNumber}.", payload.GetProperty("body").GetString());
            Assert.Equal(["bc-bus-pass"], _provider.Asked);
        }

        [Fact]
        public async Task NothingPublished_Is404()
        {
            _provider.Result = SubmissionAcknowledgementLookup.None;

            using HttpResponseMessage response = await CreateClient().GetAsync(BusPassUrl);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ContentEngineUnavailable_Is502ProblemDetails()
        {
            _provider.Result = SubmissionAcknowledgementLookup.Unavailable;

            using HttpResponseMessage response = await CreateClient().GetAsync(BusPassUrl);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(502, problem.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Content engine unavailable", problem.RootElement.GetProperty("title").GetString());
        }

        [Theory]
        [InlineData("registration")]
        [InlineData("BC-BUS-PASS")]
        [InlineData("bc%20bus-pass")]
        public async Task AFormWhosePageShowsNoMessage_Is404_WithoutReadingTheContentEngine(string formSpecId)
        {
            _provider.Result = SubmissionAcknowledgementLookup.Unavailable;

            using HttpResponseMessage response = await CreateClient().GetAsync(
                new Uri($"/v1/forms/{formSpecId}/acknowledgement", UriKind.Relative));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(_provider.Asked);
        }

        private HttpClient CreateClient()
        {
            string dbName = Guid.NewGuid().ToString();
            return _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseMockAuthSettings(allowMockAuth: "false", environmentName: "test", mockAuth: "false");
                    builder.ConfigureServices(services =>
                    {
                        DbContextOptions<FormsDbContext> options = new DbContextOptionsBuilder<FormsDbContext>()
                            .UseInMemoryDatabase(dbName)
                            .Options;
                        services.RemoveAll<DbContextOptions<FormsDbContext>>();
                        services.RemoveAll<FormsDbContext>();
                        services.AddScoped<FormsDbContext>(_ => new InMemoryFormsDbContext(options));

                        services.RemoveAll<ISubmissionAcknowledgementProvider>();
                        services.AddSingleton<ISubmissionAcknowledgementProvider>(_provider);
                    });
                })
                .CreateClient();
        }

        private sealed class FakeProvider : ISubmissionAcknowledgementProvider
        {
            public SubmissionAcknowledgementLookup Result { get; set; } = SubmissionAcknowledgementLookup.None;

            public List<string> Asked { get; } = [];

            public Task<SubmissionAcknowledgementLookup> GetLiveAsync(string formSpecId, CancellationToken cancellationToken)
            {
                this.Asked.Add(formSpecId);
                return Task.FromResult(this.Result);
            }
        }
    }
}
