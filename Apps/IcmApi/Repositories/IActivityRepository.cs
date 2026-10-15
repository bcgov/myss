namespace Icm.Api.Repositories
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>Reads ICM activities and activity plans, and creates activities, given a token.</summary>
    /// <remarks>
    /// The data-access boundary for activities, on the same terms as
    /// <see cref="IProspectRepository"/>. <see cref="Services.IActivityService"/> is the
    /// layer that obtains the token.
    /// </remarks>
    public interface IActivityRepository
    {
        /// <summary>Searches activities on the standalone path.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="query">The search. At least one criterion must be set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        /// <exception cref="System.ArgumentException">No criterion, or a blank or unsafe one. Thrown before anything is sent.</exception>
        /// <exception cref="Refit.ApiException">ICM answered with a failure status.</exception>
        /// <exception cref="IcmResponseException">ICM reported success without a usable body.</exception>
        Task<ActivityPage> SearchAsync(string bearerToken, ActivityQuery query, CancellationToken cancellationToken = default);

        /// <summary>Gets one activity by its row id.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="activityKey">The Siebel row id of the activity.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The activity, or null when the key matches nothing the caller can see.</returns>
        Task<Activity?> GetAsync(
            string bearerToken, string activityKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>Reads the activities on a service request — the events on it.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Every activity on the request, ICM's pages followed; empty when there are none the caller can see.</returns>
        Task<IReadOnlyList<Activity>> GetForServiceRequestAsync(
            string bearerToken, string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>Creates an activity profiled to a service request, case, incident or memo.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="input">The fields to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created activity as ICM stored it, with its new row id.</returns>
        /// <exception cref="System.ArgumentException">The type, the actor or the profiled-to entity is missing. Thrown before anything is sent.</exception>
        /// <exception cref="Refit.ApiException">ICM answered with a failure status.</exception>
        /// <exception cref="IcmResponseException">ICM reported success without returning the row.</exception>
        Task<Activity> CreateAsync(string bearerToken, ActivityInput input, CancellationToken cancellationToken = default);

        /// <summary>Searches activity plans on the standalone path.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="query">The search. At least one criterion must be set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        Task<ActivityPlanPage> SearchPlansAsync(string bearerToken, ActivityPlanQuery query, CancellationToken cancellationToken = default);

        /// <summary>Reads the activity plans applied to a service request.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Every plan on the request; empty when there are none the caller can see.</returns>
        Task<IReadOnlyList<ActivityPlan>> GetPlansForServiceRequestAsync(
            string bearerToken, string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default);
    }
}
