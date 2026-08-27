namespace Icm.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// ICM service requests, authenticated on the caller's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The thing to inject.</b> It gets a token, keeps using it until it is nearly
    /// expired, and calls ICM — so a caller supplies credentials once at registration and
    /// then works in service-request terms alone.
    /// </para>
    /// <para>
    /// Reach for <see cref="Repositories.IServiceRequestRepository"/> instead only when the
    /// caller already holds a token of its own — a request carrying a citizen's token, for
    /// instance, where a client-credentials token would be the wrong identity entirely.
    /// </para>
    /// </remarks>
    public interface IServiceRequestService
    {
        /// <summary>Searches service requests.</summary>
        /// <param name="query">The search parameters, or null for an unfiltered search.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        Task<ServiceRequestPage> SearchAsync(
            ServiceRequestQuery? query = null,
            CancellationToken cancellationToken = default);

        /// <summary>Gets one service request by key.</summary>
        /// <param name="serviceRequestKey">The ICM row id of the service request.</param>
        /// <param name="options">Field selection, child links and visibility mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The record, or null when there is no such record for this caller.</returns>
        Task<ServiceRequest?> GetAsync(
            string serviceRequestKey,
            ServiceRequestReadOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Creates a service request.</summary>
        /// <param name="input">The fields to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created record, including the id ICM assigned.</returns>
        Task<ServiceRequest> CreateAsync(
            ServiceRequestInput input,
            CancellationToken cancellationToken = default);

        /// <summary>Updates the service request identified by key.</summary>
        /// <param name="serviceRequestKey">The ICM row id of the service request.</param>
        /// <param name="input">The fields to change.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored record, or null when nothing changed.</returns>
        Task<ServiceRequest?> UpdateAsync(
            string serviceRequestKey,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default);

        /// <summary>Inserts or updates a service request matched on ICM's user keys.</summary>
        /// <param name="input">The fields to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored record, or null when nothing changed.</returns>
        Task<ServiceRequest?> UpsertAsync(
            ServiceRequestInput input,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes the service request identified by key.</summary>
        /// <param name="serviceRequestKey">The ICM row id of the service request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when a record was deleted; false when there was nothing to delete.</returns>
        Task<bool> DeleteAsync(
            string serviceRequestKey,
            CancellationToken cancellationToken = default);
    }
}
