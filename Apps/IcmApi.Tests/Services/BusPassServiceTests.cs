namespace Icm.Api.Tests.Services
{
    using Icm.Api.Models;
    using Icm.Api.Services;
    using Icm.Api.Tests.TestDoubles;

    /// <summary>
    /// The service adds one thing to the repository — the token — so that is what these
    /// check, exactly as for the service request service.
    /// </summary>
    public class BusPassServiceTests
    {
        private static readonly OAuthClientCredentials Credentials = new()
        {
            TokenUrl = new Uri("https://login.example.gov.bc.ca/realms/a/token"),
            ClientId = "myss-icm",
            ClientSecret = "s3cr3t",
        };

        private static BusPassApplication Application() => new()
        {
            RequestType = BusPassRequestType.NewApplication,
        };

        [Fact]
        public async Task EverySubmissionPassesTheTokenToTheRepository()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingBusPassRepository repository = new();
            BusPassService service = new(repository, tokenService, Credentials);

            await service.SubmitAsync(Application());
            await service.SubmitAsync(Application());

            Assert.Equal(2, repository.Tokens.Count);
            Assert.All(repository.Tokens, token => Assert.Equal("token-1", token));
            Assert.Equal(1, endpoint.CallCount);
        }

        [Fact]
        public void ItRefusesToBeBuiltWithoutItsCollaborators()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingBusPassRepository repository = new();

            Assert.Throws<ArgumentNullException>(
                () => new BusPassService(null!, tokenService, Credentials));
            Assert.Throws<ArgumentNullException>(
                () => new BusPassService(repository, null!, Credentials));
            Assert.Throws<ArgumentNullException>(
                () => new BusPassService(repository, tokenService, null!));
        }
    }
}
