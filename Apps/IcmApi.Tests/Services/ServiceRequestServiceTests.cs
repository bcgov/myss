namespace Icm.Api.Tests.Services
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Icm.Api.Tests.TestDoubles;

    /// <summary>
    /// The service adds one thing to the repository — the token — so that is what these
    /// check. The repository's own behaviour is covered by its own tests.
    /// </summary>
    public class ServiceRequestServiceTests
    {
        private static readonly Uri TokenUrl = new("https://login.example.gov.bc.ca/realms/a/token");

        private static readonly OAuthClientCredentials Credentials = new()
        {
            TokenUrl = TokenUrl,
            ClientId = "myss-icm",
            ClientSecret = "s3cr3t",
        };

        private static (ServiceRequestService Service, RecordingServiceRequestRepository Repository)
            Create(IOAuthTokenService tokenService)
        {
            RecordingServiceRequestRepository repository = new();
            return (new ServiceRequestService(repository, tokenService, Credentials), repository);
        }

        [Fact]
        public async Task EveryCallPassesTheTokenToTheRepository()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            (ServiceRequestService service, RecordingServiceRequestRepository repository) =
                Create(tokenService);

            await service.SearchAsync();
            await service.GetAsync("1-ABCDE");
            await service.CreateAsync(new ServiceRequestInput());
            await service.UpdateAsync("1-ABCDE", new ServiceRequestInput());
            await service.UpsertAsync(new ServiceRequestInput());
            await service.DeleteAsync("1-ABCDE");

            Assert.Equal(6, repository.Tokens.Count);
            Assert.All(repository.Tokens, token => Assert.Equal("token-1", token));
        }

        [Fact]
        public async Task TheTokenIsFetchedOnceAndThenReused()
        {
            // The caching lives in the token service, so six ICM calls are one token call.
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            (ServiceRequestService service, _) = Create(tokenService);

            for (int i = 0; i < 6; i++)
            {
                await service.SearchAsync();
            }

            Assert.Equal(1, endpoint.CallCount);
        }

        [Fact]
        public async Task ItAuthenticatesWithTheCredentialsItWasConstructedWith()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            (ServiceRequestService service, _) = Create(tokenService);

            await service.SearchAsync();

            (Uri url, OAuthClientCredentials credentials) = Assert.Single(endpoint.Calls);
            Assert.Equal(TokenUrl, url);
            Assert.Equal("myss-icm", credentials.ClientId);
        }

        [Fact]
        public void ItRefusesToBeBuiltWithoutItsCollaborators()
        {
            RecordingServiceRequestRepository repository = new();
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);

            Assert.Throws<ArgumentNullException>(
                () => new ServiceRequestService(null!, tokenService, Credentials));
            Assert.Throws<ArgumentNullException>(
                () => new ServiceRequestService(repository, null!, Credentials));
            Assert.Throws<ArgumentNullException>(
                () => new ServiceRequestService(repository, tokenService, null!));
        }
    }
}
