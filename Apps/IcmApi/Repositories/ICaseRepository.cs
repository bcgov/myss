namespace Icm.Api.Repositories
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>Reads ICM cases, given a token.</summary>
    /// <remarks>
    /// The data-access boundary for cases, on the same terms as
    /// <see cref="IContactRepository"/>: models in and out, nothing of Siebel's above it,
    /// the token a parameter because ICM applies the calling identity's visibility.
    /// <see cref="Services.ICaseService"/> is the layer that obtains the token; use that
    /// unless the caller already holds one of its own.
    /// </remarks>
    public interface ICaseRepository
    {
        /// <summary>Searches cases.</summary>
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
        Task<CasePage> SearchAsync(
            string bearerToken,
            CaseQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Gets one case by its row id.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="caseKey">The Siebel row id of the case — <see cref="Case.Id"/>, not the case number.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The case, or null when the key matches nothing the caller can see.</returns>
        /// <exception cref="Refit.ApiException">ICM answered with a failure status.</exception>
        /// <exception cref="IcmResponseException">ICM reported success without a usable body.</exception>
        Task<Case?> GetAsync(
            string bearerToken,
            string caseKey,
            CaseReadOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the people on a case, with their relationship to it.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="caseKey">The Siebel row id of the case.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The people on the case; empty when there are none the caller can see, which includes "no such case".</returns>
        /// <exception cref="Refit.ApiException">ICM answered with a failure status.</exception>
        /// <exception cref="IcmResponseException">ICM reported success without a usable body.</exception>
        Task<IReadOnlyList<CaseContact>> GetContactsAsync(
            string bearerToken,
            string caseKey,
            CaseReadOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
