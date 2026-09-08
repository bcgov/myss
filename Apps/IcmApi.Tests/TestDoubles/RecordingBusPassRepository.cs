namespace Icm.Api.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Records the token each submission arrived with, answering with an empty result —
    /// the same shape as <see cref="RecordingServiceRequestRepository"/>, for the same
    /// reason.
    /// </summary>
    internal sealed class RecordingBusPassRepository : IBusPassRepository
    {
        private readonly List<string> _tokens = [];

        public IReadOnlyList<string> Tokens => _tokens;

        public Task<BusPassResult> SubmitAsync(
            string bearerToken,
            BusPassApplication application,
            CancellationToken cancellationToken = default)
        {
            _tokens.Add(bearerToken);
            return Task.FromResult(new BusPassResult());
        }
    }
}
