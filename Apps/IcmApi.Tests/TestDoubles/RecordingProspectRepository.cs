namespace Icm.Api.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>Records the token and arguments each call arrived with.</summary>
    internal sealed class RecordingProspectRepository : IProspectRepository
    {
        private readonly List<(string Token, string Method, object? Argument, ProspectReadOptions? Options)> _calls = [];

        public IReadOnlyList<(string Token, string Method, object? Argument, ProspectReadOptions? Options)> Calls => _calls;

        public ProspectPage SearchResult { get; set; } = new();

        public Prospect? GetResult { get; set; }

        public IReadOnlyList<Prospect> ChildResult { get; set; } = [];

        public Prospect CreateResult { get; set; } = new();

        public Task<ProspectPage> SearchAsync(
            string bearerToken, ProspectQuery query, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(SearchAsync), query, null));
            return Task.FromResult(SearchResult);
        }

        public Task<Prospect?> GetAsync(
            string bearerToken, string prospectKey, ProspectReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(GetAsync), prospectKey, options));
            return Task.FromResult(GetResult);
        }

        public Task<IReadOnlyList<Prospect>> GetForServiceRequestAsync(
            string bearerToken, string serviceRequestKey, ProspectReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(GetForServiceRequestAsync), serviceRequestKey, options));
            return Task.FromResult(ChildResult);
        }

        public Task<Prospect> CreateAsync(
            string bearerToken, ProspectInput input, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(CreateAsync), input, null));
            return Task.FromResult(CreateResult);
        }
    }
}
