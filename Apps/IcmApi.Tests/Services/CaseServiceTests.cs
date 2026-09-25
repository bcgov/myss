namespace Icm.Api.Tests.Services
{
    using Icm.Api.Models;
    using Icm.Api.Services;
    using Icm.Api.Tests.TestDoubles;

    /// <summary>
    /// The service adds one thing to the repository — the token — so that is what these
    /// check.
    /// </summary>
    public class CaseServiceTests
    {
        private static readonly OAuthClientCredentials Credentials = new()
        {
            TokenUrl = new Uri("https://login.example.gov.bc.ca/realms/a/token"),
            ClientId = "myss-icm",
            ClientSecret = "s3cr3t",
        };

        private static readonly CaseQuery Query = new() { CaseNumber = "1-11077140770" };

        [Fact]
        public async Task EveryReadPassesTheTokenAndItsArgumentsThrough()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            CaseReadOptions options = new() { ViewMode = "Group" };
            RecordingCaseRepository repository = new()
            {
                SearchResult = new CasePage { Items = [new Case { Id = "1-5371KIQ" }] },
                GetResult = new Case { Id = "1-5371KIQ" },
                ContactsResult = [new CaseContact { Id = "1-532MU4J" }],
            };
            CaseService service = new(repository, tokenService, Credentials);

            Assert.Equal("1-5371KIQ", Assert.Single((await service.SearchAsync(Query)).Items).Id);
            Assert.Equal("1-5371KIQ", (await service.GetAsync("1-5371KIQ", options))!.Id);
            Assert.Equal("1-532MU4J", Assert.Single(await service.GetContactsAsync("1-5371KIQ", options)).Id);

            Assert.Equal(3, repository.Calls.Count);
            Assert.All(repository.Calls, call => Assert.Equal("token-1", call.Token));
            Assert.Same(Query, repository.Calls[0].Argument);
            Assert.Equal("1-5371KIQ", repository.Calls[1].Argument);
            Assert.Same(options, repository.Calls[1].Options);
            Assert.Same(options, repository.Calls[2].Options);
            Assert.Equal(1, endpoint.CallCount);
        }

        [Fact]
        public async Task ARefusedArgumentCostsNoTokenRequest()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            CaseService service = new(new RecordingCaseRepository(), tokenService, Credentials);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SearchAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(" "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetContactsAsync(string.Empty));
            Assert.Equal(0, endpoint.CallCount);
        }

        [Fact]
        public void TheConstructorRefusesMissingCollaborators()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingCaseRepository repository = new();

            Assert.Throws<ArgumentNullException>(() => new CaseService(null!, tokenService, Credentials));
            Assert.Throws<ArgumentNullException>(() => new CaseService(repository, null!, Credentials));
            Assert.Throws<ArgumentNullException>(() => new CaseService(repository, tokenService, null!));
        }
    }
}
