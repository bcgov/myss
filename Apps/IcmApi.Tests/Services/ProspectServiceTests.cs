namespace Icm.Api.Tests.Services
{
    using Icm.Api.Models;
    using Icm.Api.Services;
    using Icm.Api.Tests.TestDoubles;

    /// <summary>
    /// The service adds one thing to the repository — the token — so that is what these
    /// check.
    /// </summary>
    public class ProspectServiceTests
    {
        private static readonly OAuthClientCredentials Credentials = new()
        {
            TokenUrl = new Uri("https://login.example.gov.bc.ca/realms/a/token"),
            ClientId = "myss-icm",
            ClientSecret = "s3cr3t",
        };

        private static readonly ProspectQuery Query = new() { ServiceRequestId = "1-53CJXK6" };

        private static ProspectInput Input => new()
        {
            ServiceRequestId = "1-53CJXK6",
            FirstName = "Myss",
            LastName = "IntegrationTest",
            BirthDate = new DateOnly(2000, 1, 25),
            Gender = "Man/Boy",
        };

        [Fact]
        public async Task EveryCallPassesTheTokenAndItsArgumentsThrough()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            ProspectReadOptions options = new() { ViewMode = "Manager" };
            ProspectInput input = Input;
            RecordingProspectRepository repository = new()
            {
                SearchResult = new ProspectPage { Items = [new Prospect { Id = "1-53CJXKH" }] },
                GetResult = new Prospect { Id = "1-53CJXKH" },
                ChildResult = [new Prospect { Id = "1-53CJXKH" }],
                CreateResult = new Prospect { Id = "1-NEW" },
            };
            ProspectService service = new(repository, tokenService, Credentials);

            Assert.Equal("1-53CJXKH", Assert.Single((await service.SearchAsync(Query)).Items).Id);
            Assert.Equal("1-53CJXKH", (await service.GetAsync("1-53CJXKH", options))!.Id);
            Assert.Equal("1-53CJXKH", Assert.Single(await service.GetForServiceRequestAsync("1-53CJXK6", options)).Id);
            Assert.Equal("1-NEW", (await service.CreateAsync(input)).Id);

            Assert.Equal(4, repository.Calls.Count);
            Assert.All(repository.Calls, call => Assert.Equal("token-1", call.Token));
            Assert.Same(Query, repository.Calls[0].Argument);
            Assert.Equal("1-53CJXKH", repository.Calls[1].Argument);
            Assert.Same(options, repository.Calls[1].Options);
            Assert.Same(options, repository.Calls[2].Options);
            Assert.Same(input, repository.Calls[3].Argument);
            Assert.Equal(1, endpoint.CallCount);
        }

        [Fact]
        public async Task ARefusedArgumentCostsNoTokenRequest()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingProspectRepository repository = new();
            ProspectService service = new(repository, tokenService, Credentials);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SearchAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new ProspectQuery()));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new ProspectQuery { Source = "x\" OR [Id] LIKE \"*" }));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(" "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetForServiceRequestAsync(string.Empty));
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new ProspectInput { FirstName = "F", LastName = "L" }));

            Assert.Equal(0, endpoint.CallCount);
            Assert.Empty(repository.Calls);
        }

        [Fact]
        public void TheConstructorRefusesMissingCollaborators()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingProspectRepository repository = new();

            Assert.Throws<ArgumentNullException>(() => new ProspectService(null!, tokenService, Credentials));
            Assert.Throws<ArgumentNullException>(() => new ProspectService(repository, null!, Credentials));
            Assert.Throws<ArgumentNullException>(() => new ProspectService(repository, tokenService, null!));
        }
    }
}
