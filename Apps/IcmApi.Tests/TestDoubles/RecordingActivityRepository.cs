namespace Icm.Api.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>Records the token and arguments each call arrived with.</summary>
    internal sealed class RecordingActivityRepository : IActivityRepository
    {
        private readonly List<(string Token, string Method, object? Argument, ActivityReadOptions? Options)> _calls = [];

        public IReadOnlyList<(string Token, string Method, object? Argument, ActivityReadOptions? Options)> Calls => _calls;

        public ActivityPage SearchResult { get; set; } = new();

        public Activity? GetResult { get; set; }

        public IReadOnlyList<Activity> ChildResult { get; set; } = [];

        public Activity CreateResult { get; set; } = new();

        public ActivityPlanPage PlanSearchResult { get; set; } = new();

        public IReadOnlyList<ActivityPlan> PlanChildResult { get; set; } = [];

        public Task<ActivityPage> SearchAsync(string bearerToken, ActivityQuery query, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(SearchAsync), query, null));
            return Task.FromResult(SearchResult);
        }

        public Task<Activity?> GetAsync(string bearerToken, string activityKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(GetAsync), activityKey, options));
            return Task.FromResult(GetResult);
        }

        public Task<IReadOnlyList<Activity>> GetForServiceRequestAsync(
            string bearerToken, string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(GetForServiceRequestAsync), serviceRequestKey, options));
            return Task.FromResult(ChildResult);
        }

        public Task<Activity> CreateAsync(string bearerToken, ActivityInput input, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(CreateAsync), input, null));
            return Task.FromResult(CreateResult);
        }

        public Task<ActivityPlanPage> SearchPlansAsync(string bearerToken, ActivityPlanQuery query, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(SearchPlansAsync), query, null));
            return Task.FromResult(PlanSearchResult);
        }

        public Task<IReadOnlyList<ActivityPlan>> GetPlansForServiceRequestAsync(
            string bearerToken, string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            _calls.Add((bearerToken, nameof(GetPlansForServiceRequestAsync), serviceRequestKey, options));
            return Task.FromResult(PlanChildResult);
        }
    }
}
