namespace Icm.Api.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>ICM activities and activity plans — the events on a service request — authenticated on the caller's behalf.</summary>
    /// <remarks>
    /// <b>The thing to inject.</b> Reads the activities and plans on a service request (what
    /// the bus pass workflow's activity plan produces), searches either, and creates an
    /// activity profiled to a service request, case, incident or memo. Plans cannot be
    /// created through the REST API as it stands.
    /// </remarks>
    public interface IActivityService
    {
        /// <summary>Searches activities.</summary>
        /// <param name="query">The search. At least one criterion must be set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        Task<ActivityPage> SearchAsync(ActivityQuery query, CancellationToken cancellationToken = default);

        /// <summary>Gets one activity by its row id.</summary>
        /// <param name="activityKey">The Siebel row id of the activity.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The activity, or null when the key matches nothing the caller can see.</returns>
        Task<Activity?> GetAsync(string activityKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>Reads the activities on a service request — the events on it.</summary>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Every activity on the request; empty when there are none the caller can see.</returns>
        Task<IReadOnlyList<Activity>> GetForServiceRequestAsync(
            string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>Creates an activity profiled to a service request, case, incident or memo.</summary>
        /// <param name="input">The fields to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created activity as ICM stored it.</returns>
        /// <exception cref="System.ArgumentException">The type, the actor or the profiled-to entity is missing.</exception>
        Task<Activity> CreateAsync(ActivityInput input, CancellationToken cancellationToken = default);

        /// <summary>Searches activity plans.</summary>
        /// <param name="query">The search. At least one criterion must be set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        Task<ActivityPlanPage> SearchPlansAsync(ActivityPlanQuery query, CancellationToken cancellationToken = default);

        /// <summary>Reads the activity plans applied to a service request.</summary>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Every plan on the request; empty when there are none the caller can see.</returns>
        Task<IReadOnlyList<ActivityPlan>> GetPlansForServiceRequestAsync(
            string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default);
    }
}
