namespace Icm.Api.Repositories
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// Reads and writes ICM service requests, given a token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The data-access boundary. Everything above it works in
    /// <see cref="Icm.Api.Models"/> types and never sees Siebel's field names, its
    /// <c>"Y"</c>/<c>"N"</c> flags, or the difference between a <c>204</c> and a
    /// <c>404</c>. Everything below is Refit and the wire contracts, and is internal.
    /// </para>
    /// <para>
    /// <b>Missing is null or empty; anything else throws.</b> ICM reports "found nothing"
    /// with a <c>204</c> on some operations and a <c>404</c> on others, and neither is a
    /// failure. A real failure — bad credentials, a rejected write, ICM being down — comes
    /// out as a <see cref="Refit.ApiException"/>.
    /// </para>
    /// <para>
    /// The token is a parameter because ICM applies the calling user's Siebel visibility to
    /// every read and write. <see cref="Services.IServiceRequestService"/> is the layer that
    /// obtains it; use that unless the caller already holds a token of its own.
    /// </para>
    /// </remarks>
    public interface IServiceRequestRepository
    {
        /// <summary>Searches service requests.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="query">The search parameters, or null for an unfiltered search.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        Task<ServiceRequestPage> SearchAsync(
            string bearerToken,
            ServiceRequestQuery? query = null,
            CancellationToken cancellationToken = default);

        /// <summary>Gets one service request by key.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="serviceRequestKey">The ICM row id of the service request.</param>
        /// <param name="options">Field selection, child links and visibility mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The record, or null when the key matches nothing the caller is allowed to see —
        /// which ICM does not distinguish from nothing existing at all.
        /// </returns>
        Task<ServiceRequest?> GetAsync(
            string bearerToken,
            string serviceRequestKey,
            ServiceRequestReadOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Creates a service request.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="input">The fields to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created record, including the id ICM assigned.</returns>
        Task<ServiceRequest> CreateAsync(
            string bearerToken,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default);

        /// <summary>Updates the service request identified by key.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="serviceRequestKey">The ICM row id of the service request.</param>
        /// <param name="input">
        /// The fields to change. Only the properties that are set are sent.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The stored record, or null when there was nothing to report — ICM answered
        /// <c>304 Not Modified</c> because nothing changed, or one of its documented
        /// no-resource statuses because the key matched nothing.
        /// </returns>
        Task<ServiceRequest?> UpdateAsync(
            string bearerToken,
            string serviceRequestKey,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts or updates without naming a key — ICM matches an existing record on the
        /// business component's user keys and creates one when it finds none.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="input">The fields to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The stored record, or null when ICM answered <c>304 Not Modified</c> or a
        /// documented no-resource status.
        /// </returns>
        /// <remarks>
        /// Prefer <see cref="UpdateAsync"/> when the key is known: which record an upsert
        /// lands on depends on ICM's user-key configuration rather than on anything visible
        /// in the call.
        /// </remarks>
        Task<ServiceRequest?> UpsertAsync(
            string bearerToken,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes the service request identified by key.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="serviceRequestKey">The ICM row id of the service request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// True when a record was deleted; false when there was nothing to delete.
        /// </returns>
        Task<bool> DeleteAsync(
            string bearerToken,
            string serviceRequestKey,
            CancellationToken cancellationToken = default);
    }
}
