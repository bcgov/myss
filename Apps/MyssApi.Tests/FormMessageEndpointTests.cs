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
    /// The form message endpoint over real HTTP: anonymous, the three texts and nothing
    /// more, and a status for each other outcome.
    /// </summary>
    public class FormMessageEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private static readonly Uri BusPassUrl = new("/v1/forms/bc-bus-pass/messages/confirmation", UriKind.Relative);

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="FormMessageEndpointTests"/> class.</summary>
        /// <param name="factory">The shared host factory.</param>
        public FormMessageEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AnAnonymousCaller_GetsTheThreeTexts_AndNothingElse()
        {
            _provider.Result = FormMessageLookup.Found(new FormMessageModel
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
            Assert.Equal(["bc-bus-pass/confirmation"], _provider.Asked);
        }

        [Fact]
        public async Task NothingPublished_Is404()
        {
            _provider.Result = FormMessageLookup.None;

            using HttpResponseMessage response = await CreateClient().GetAsync(BusPassUrl);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ContentEngineUnavailable_Is502ProblemDetails()
        {
            _provider.Result = FormMessageLookup.Unavailable;

            using HttpResponseMessage response = await CreateClient().GetAsync(BusPassUrl);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(502, problem.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Content engine unavailable", problem.RootElement.GetProperty("title").GetString());
        }

        [Theory]
        [InlineData("registration", "confirmation")]
        [InlineData("BC-BUS-PASS", "confirmation")]
        [InlineData("bc%20bus-pass", "confirmation")]
        [InlineData("bc-bus-pass", "intro")]
        [InlineData("bc-bus-pass", "Confirmation")]
        public async Task AMessageThatIsNotDefined_Is404_WithoutReadingTheContentEngine(string formSpecId, string messageKey)
        {
            _provider.Result = FormMessageLookup.Unavailable;

            using HttpResponseMessage response = await CreateClient().GetAsync(
                new Uri($"/v1/forms/{formSpecId}/messages/{messageKey}", UriKind.Relative));

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

                        services.RemoveAll<IFormMessageProvider>();
                        services.AddSingleton<IFormMessageProvider>(_provider);
                    });
                })
                .CreateClient();
        }

        private sealed class FakeProvider : IFormMessageProvider
        {
            public FormMessageLookup Result { get; set; } = FormMessageLookup.None;

            public List<string> Asked { get; } = [];

            public Task<FormMessageLookup> GetLiveAsync(string formSpecId, string messageKey, CancellationToken cancellationToken)
            {
                this.Asked.Add($"{formSpecId}/{messageKey}");
                return Task.FromResult(this.Result);
            }
        }
    }
}
