namespace Icm.Api.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>Records the token and query each call arrived with.</summary>
    internal sealed class RecordingContactRepository : IContactRepository
    {
        private readonly List<(string Token, ContactQuery Query)> _calls = [];

        public IReadOnlyList<(string Token, ContactQuery Query)> Calls => _calls;

        public ContactPage Result { get; set; } = new();

        public Task<ContactPage> SearchAsync(
            string bearerToken,
            ContactQuery query,
            CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, query));
            return Task.FromResult(Result);
        }
    }
}
