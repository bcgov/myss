namespace Icm.Api.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Stands in for the token repository, recording what it was asked for. Lets the
    /// caching tests count round trips without a transport in sight — which is the payoff
    /// of the service depending on the repository rather than on Refit.
    /// </summary>
    internal sealed class FakeTokenRepository : IOAuthTokenRepository
    {
        private readonly List<(Uri Url, OAuthClientCredentials Request)> _calls = [];
        private readonly Lock _sync = new();
        private int _tokenCounter;

        /// <summary>Gets or sets the lifetime reported on each issued token, in seconds.</summary>
        public int? ExpiresIn { get; set; } = 300;

        /// <summary>
        /// Gets or sets a hook awaited before each response, used to hold requests open
        /// while a concurrency test lines callers up behind them.
        /// </summary>
        public Func<Task>? BeforeResponding { get; set; }

        /// <summary>
        /// Gets or sets a replacement response builder. Null issues a fresh numbered token.
        /// </summary>
        public Func<Uri, OAuthClientCredentials, AccessToken>? Responder { get; set; }

        /// <summary>Gets or sets an exception to throw instead of responding.</summary>
        public Exception? Throws { get; set; }

        /// <summary>Gets every call the service made, in order.</summary>
        public IReadOnlyList<(Uri Url, OAuthClientCredentials Request)> Calls
        {
            get
            {
                lock (_sync)
                {
                    return [.. _calls];
                }
            }
        }

        /// <summary>Gets the number of calls made.</summary>
        public int CallCount
        {
            get
            {
                lock (_sync)
                {
                    return _calls.Count;
                }
            }
        }

        public async Task<AccessToken> GetTokenAsync(
            OAuthClientCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _calls.Add((credentials.TokenUrl!, credentials));
            }

            if (BeforeResponding is { } hook)
            {
                await hook().ConfigureAwait(false);
            }

            if (Throws is { } exception)
            {
                throw exception;
            }

            if (Responder is { } responder)
            {
                return responder(credentials.TokenUrl!, credentials);
            }

            int number = Interlocked.Increment(ref _tokenCounter);
            return new AccessToken(
                $"token-{number}",
                ExpiresIn is { } seconds ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero);
        }
    }
}
