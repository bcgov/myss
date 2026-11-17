namespace Icm.Api.Repositories
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>Reads and creates ICM prospects — the applicant rows on a service request — given a token.</summary>
    /// <remarks>
    /// The data-access boundary for prospects, on the same terms as
    /// <see cref="ICaseRepository"/>: models in and out, nothing of Siebel's above it, the
    /// token a parameter because ICM applies the calling identity's visibility.
    /// <see cref="Services.IProspectService"/> is the layer that obtains the token.
    /// </remarks>
    public interface IProspectRepository
    {
        /// <summary>Searches prospects on the standalone path (<c>data/Prospects/SRProspects</c>).</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="query">The search. At least one criterion must be set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        /// <exception cref="System.ArgumentException">
        /// The query has no criterion, or one that is blank or not safe to search with.
        /// Thrown before anything is sent.
        /// </exception>
        /// <exception cref="Refit.ApiException">ICM answered with a failure status.</exception>
        /// <exception cref="IcmResponseException">ICM reported success without a usable body.</exception>
        Task<ProspectPage> SearchAsync(
            string bearerToken,
            ProspectQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Gets one prospect by its row id, on the standalone path.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="prospectKey">The Siebel row id of the prospect — <see cref="Prospect.Id"/>.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The prospect, or null when the key matches nothing the caller can see.</returns>
        /// <exception cref="Refit.ApiException">ICM answered with a failure status.</exception>
        /// <exception cref="IcmResponseException">ICM reported success without a usable body.</exception>
        Task<Prospect?> GetAsync(
            string bearerToken,
            string prospectKey,
            ProspectReadOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the prospects on a service request — its <c>SRProspects</c> child collection.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Every prospect on the request — ICM's pages are followed — and empty when there
        /// are none the caller can see, which includes "no such service request".
        /// </returns>
        /// <exception cref="Refit.ApiException">ICM answered with a failure status.</exception>
        /// <exception cref="IcmResponseException">
        /// ICM reported success without a usable body, or never reported a last page.
        /// </exception>
        Task<IReadOnlyList<Prospect>> GetForServiceRequestAsync(
            string bearerToken,
            string serviceRequestKey,
            ProspectReadOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a prospect, linked to the service request
        /// <see cref="ProspectInput.ServiceRequestId"/> names.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="input">The fields to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created prospect as ICM stored it, with its new row id.</returns>
        /// <exception cref="System.ArgumentException">
        /// A required field — the names, birth date, gender — or the service request id is
        /// missing. Thrown before anything is sent.
        /// </exception>
        /// <exception cref="Refit.ApiException">ICM answered with a failure status.</exception>
        /// <exception cref="IcmResponseException">ICM reported success without returning the row.</exception>
        Task<Prospect> CreateAsync(
            string bearerToken,
            ProspectInput input,
            CancellationToken cancellationToken = default);
    }
}
