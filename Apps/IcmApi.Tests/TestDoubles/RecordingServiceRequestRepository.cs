namespace Icm.Api.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Records the token each call arrived with. Everything else answers with an empty
    /// result — the repository's real behaviour is tested against the real Refit stack
    /// elsewhere, so reproducing any of it here would only be a second thing to keep true.
    /// </summary>
    internal sealed class RecordingServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<string> _tokens = [];

        public IReadOnlyList<string> Tokens => _tokens;

        public Task<ServiceRequestPage> SearchAsync(
            string bearerToken,
            ServiceRequestQuery? query = null,
            CancellationToken cancellationToken = default)
        {
            _tokens.Add(bearerToken);
            return Task.FromResult(new ServiceRequestPage());
        }

        public Task<ServiceRequest?> GetAsync(
            string bearerToken,
            string serviceRequestKey,
            ServiceRequestReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            _tokens.Add(bearerToken);
            return Task.FromResult<ServiceRequest?>(null);
        }

        public Task<ServiceRequest> CreateAsync(
            string bearerToken,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default)
        {
            _tokens.Add(bearerToken);
            return Task.FromResult(new ServiceRequest());
        }

        public Task<ServiceRequest?> UpdateAsync(
            string bearerToken,
            string serviceRequestKey,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default)
        {
            _tokens.Add(bearerToken);
            return Task.FromResult<ServiceRequest?>(null);
        }

        public Task<ServiceRequest?> UpsertAsync(
            string bearerToken,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default)
        {
            _tokens.Add(bearerToken);
            return Task.FromResult<ServiceRequest?>(null);
        }

        public Task<bool> DeleteAsync(
            string bearerToken,
            string serviceRequestKey,
            CancellationToken cancellationToken = default)
        {
            _tokens.Add(bearerToken);
            return Task.FromResult(false);
        }
    }
}
