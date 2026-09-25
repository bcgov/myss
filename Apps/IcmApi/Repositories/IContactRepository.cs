namespace Icm.Api.Repositories
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// Searches ICM contacts, given a token.
    /// </summary>
    /// <remarks>
    /// The data-access boundary for contacts, on the same terms as
    /// <see cref="IServiceRequestRepository"/>: models in and out, nothing of Siebel's
    /// above it, the token a parameter because ICM applies the calling identity's
    /// visibility. <see cref="Services.IContactService"/> is the layer that obtains the
    /// token; use that unless the caller already holds one of its own.
    /// </remarks>
    public interface IContactRepository
    {
        /// <summary>Searches contacts.</summary>
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
        Task<ContactPage> SearchAsync(
            string bearerToken,
            ContactQuery query,
            CancellationToken cancellationToken = default);
    }
}
