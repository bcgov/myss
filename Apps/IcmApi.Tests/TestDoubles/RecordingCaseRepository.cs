namespace Icm.Api.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>Records the token and arguments each call arrived with.</summary>
    internal sealed class RecordingCaseRepository : ICaseRepository
    {
        private readonly List<(string Token, string Method, object? Argument, CaseReadOptions? Options)> _calls = [];

        public IReadOnlyList<(string Token, string Method, object? Argument, CaseReadOptions? Options)> Calls => _calls;

        public CasePage SearchResult { get; set; } = new();

        public Case? GetResult { get; set; }

        public IReadOnlyList<CaseContact> ContactsResult { get; set; } = [];

        public Task<CasePage> SearchAsync(
            string bearerToken, CaseQuery query, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(SearchAsync), query, null));
            return Task.FromResult(SearchResult);
        }

        public Task<Case?> GetAsync(
            string bearerToken, string caseKey, CaseReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(GetAsync), caseKey, options));
            return Task.FromResult(GetResult);
        }

        public Task<IReadOnlyList<CaseContact>> GetContactsAsync(
            string bearerToken, string caseKey, CaseReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(GetContactsAsync), caseKey, options));
            return Task.FromResult(ContactsResult);
        }
    }
}
