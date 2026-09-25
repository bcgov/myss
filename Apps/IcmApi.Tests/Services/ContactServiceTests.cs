namespace Icm.Api.Tests.Services
{
    using Icm.Api.Models;
    using Icm.Api.Services;
    using Icm.Api.Tests.TestDoubles;

    /// <summary>
    /// The service adds one thing to the repository — the token — so that is what these
    /// check.
    /// </summary>
    public class ContactServiceTests
    {
        private static readonly OAuthClientCredentials Credentials = new()
        {
            TokenUrl = new Uri("https://login.example.gov.bc.ca/realms/a/token"),
            ClientId = "myss-icm",
            ClientSecret = "s3cr3t",
        };

        private static readonly ContactQuery Query = new() { Sin = "046454286", LastName = "IntegrationTest" };

        [Fact]
        public async Task SearchAsync_PassesTheTokenAndTheQueryThrough()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingContactRepository repository = new()
            {
                Result = new ContactPage { Items = [new Contact { Id = "1-TEST01" }] },
            };
            ContactService service = new(repository, tokenService, Credentials);

            ContactPage page = await service.SearchAsync(Query);

            (string token, ContactQuery passed) = Assert.Single(repository.Calls);
            Assert.Equal("token-1", token);
            Assert.Same(Query, passed);
            Assert.Equal("1-TEST01", Assert.Single(page.Items).Id);
        }

        [Fact]
        public async Task TheTokenIsFetchedOnceAndThenReused()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            ContactService service = new(new RecordingContactRepository(), tokenService, Credentials);

            for (int i = 0; i < 4; i++)
            {
                await service.SearchAsync(Query);
            }

            Assert.Equal(1, endpoint.CallCount);
        }

        [Fact]
        public async Task ANullQueryCostsNoTokenRequest()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            ContactService service = new(new RecordingContactRepository(), tokenService, Credentials);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SearchAsync(null!));
            Assert.Equal(0, endpoint.CallCount);
        }

        [Fact]
        public void TheConstructorRefusesMissingCollaborators()
        {
            FakeTokenRepository endpoint = new();
            using OAuthTokenService tokenService = new(endpoint);
            RecordingContactRepository repository = new();

            Assert.Throws<ArgumentNullException>(() => new ContactService(null!, tokenService, Credentials));
            Assert.Throws<ArgumentNullException>(() => new ContactService(repository, null!, Credentials));
            Assert.Throws<ArgumentNullException>(() => new ContactService(repository, tokenService, null!));
        }
    }
}
