namespace Icm.Api.Tests.Services
{
    using Icm.Api.Models;
    using Icm.Api.Services;
    using Icm.Api.Tests.TestDoubles;

    /// <summary>The service adds one thing to the repository — the token — so that is what these check.</summary>
    public class ActivityServiceTests
    {
        private static readonly OAuthClientCredentials Credentials = new()
        {
            TokenUrl = new Uri("https://login.example.gov.bc.ca/realms/a/token"), ClientId = "myss-icm", ClientSecret = "s3cr3t",
        };

        [Fact]
        public async Task EveryCallPassesTheTokenAndItsArgumentsThrough()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            ActivityQuery query = new() { ServiceRequestId = "1-53CJXO6" };
            ActivityPlanQuery planQuery = new() { TemplateId = "1-30LDVGD" };
            ActivityInput input = new() { ServiceRequestId = "1-53MU3X7", Type = "Task", ActionBy = "Staff" };
            ActivityReadOptions options = new() { ViewMode = "Manager" };
            RecordingActivityRepository repository = new()
            {
                SearchResult = new ActivityPage { Items = [new Activity { Id = "1-A" }] },
                GetResult = new Activity { Id = "1-A" },
                ChildResult = [new Activity { Id = "1-A" }],
                CreateResult = new Activity { Id = "1-NEW" },
                PlanSearchResult = new ActivityPlanPage { Items = [new ActivityPlan { Id = "1-P" }] },
                PlanChildResult = [new ActivityPlan { Id = "1-P" }],
            };
            ActivityService service = new(repository, tokenService, Credentials);

            Assert.Equal("1-A", Assert.Single((await service.SearchAsync(query)).Items).Id);
            Assert.Equal("1-A", (await service.GetAsync("1-A", options))!.Id);
            Assert.Equal("1-A", Assert.Single(await service.GetForServiceRequestAsync("1-53CJXO6", options)).Id);
            Assert.Equal("1-NEW", (await service.CreateAsync(input)).Id);
            Assert.Equal("1-P", Assert.Single((await service.SearchPlansAsync(planQuery)).Items).Id);
            Assert.Equal("1-P", Assert.Single(await service.GetPlansForServiceRequestAsync("1-53CJXO6", options)).Id);

            Assert.Equal(6, repository.Calls.Count);
            Assert.All(repository.Calls, call => Assert.Equal("token-1", call.Token));
            Assert.Same(query, repository.Calls[0].Argument);
            Assert.Same(options, repository.Calls[1].Options);
            Assert.Same(input, repository.Calls[3].Argument);
            Assert.Same(planQuery, repository.Calls[4].Argument);
            Assert.Equal(1, endpoint.CallCount);
        }

        [Fact]
        public async Task ARefusedArgumentCostsNoTokenRequest()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingActivityRepository repository = new();
            ActivityService service = new(repository, tokenService, Credentials);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SearchAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new ActivityQuery()));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(" "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetForServiceRequestAsync(string.Empty));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new ActivityInput { Type = "Task", ActionBy = "Staff" }));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchPlansAsync(new ActivityPlanQuery { Status = "x\" OR [Id] LIKE \"*" }));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetPlansForServiceRequestAsync(" "));

            Assert.Equal(0, endpoint.CallCount);
            Assert.Empty(repository.Calls);
        }

        [Fact]
        public void TheConstructorRefusesMissingCollaborators()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingActivityRepository repository = new();

            Assert.Throws<ArgumentNullException>(() => new ActivityService(null!, tokenService, Credentials));
            Assert.Throws<ArgumentNullException>(() => new ActivityService(repository, null!, Credentials));
            Assert.Throws<ArgumentNullException>(() => new ActivityService(repository, tokenService, null!));
        }
    }
}
