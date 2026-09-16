namespace Icm.Api.Host.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Services;

    /// <summary>A token service that hands out a fixed token or fails as told.</summary>
    public sealed class FakeOAuthTokenService : IOAuthTokenService
    {
        public List<OAuthClientCredentials> Requests { get; } = [];

        public string Token { get; set; } = "tok-1";

        public Exception? Failure { get; set; }

        public Task<string> GetTokenAsync(OAuthClientCredentials credentials, CancellationToken cancellationToken = default)
        {
            Requests.Add(credentials);
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(Token);
        }

        public void Clear()
        {
        }
    }
}
