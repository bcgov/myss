namespace Icm.Api.Repositories
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Refit;

    /// <summary>
    /// <see cref="IServiceRequestRepository"/> over ICM's Siebel REST API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ICM user every call acts as is fixed for the lifetime of the repository, because
    /// it identifies <i>this application's</i> ICM service account. If MySS ever needs to
    /// act as different ICM users per request, this moves to a per-call value alongside the
    /// bearer token — a contained change, since the transport already takes it per call.
    /// </para>
    /// Thin by design: call, check the status, map. Anything more interesting than that
    /// belongs above it in a service, and anything to do with how the request is shaped
    /// belongs below it in <see cref="IServiceRequestApi"/> and the wire contracts.
    /// </remarks>
    public class ServiceRequestRepository : IServiceRequestRepository
    {
        private readonly IServiceRequestApi _api;
        private readonly string? _trustedUserName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRequestRepository"/> class.
        /// </summary>
        /// <param name="httpClient">
        /// The client to send on. Its <see cref="HttpClient.BaseAddress"/> must be the ICM
        /// base URL including the version prefix, e.g.
        /// <c>https://icmsit1.api.gov.bc.ca/gov/v1.0</c>.
        /// </param>
        /// <param name="trustedUserName">
        /// The ICM user every call through this repository acts as, sent as
        /// <c>X-ICM-TrustedUserName</c>. Null sends no header.
        /// </param>
        /// <remarks>
        /// Takes an <see cref="HttpClient"/> rather than the Refit interface because that
        /// interface is internal — the transport is not something a caller should be able to
        /// swap or reach past. Register this with <c>IHttpClientFactory</c> so the handler
        /// pooling and any retry or logging handlers configured for ICM apply here.
        /// </remarks>
        public ServiceRequestRepository(HttpClient httpClient, string? trustedUserName = null)
            : this(
                RestService.For<IServiceRequestApi>(httpClient, IcmRefitSettings.Create()),
                trustedUserName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRequestRepository"/> class
        /// over a specific transport. For tests.
        /// </summary>
        /// <param name="api">The transport.</param>
        /// <param name="trustedUserName">The ICM user calls act as.</param>
        internal ServiceRequestRepository(IServiceRequestApi api, string? trustedUserName = null)
        {
            _api = api;
            _trustedUserName = trustedUserName;
        }

        /// <inheritdoc/>
        public async Task<ServiceRequestPage> SearchAsync(
            string bearerToken,
            ServiceRequestQuery? query = null,
            CancellationToken cancellationToken = default)
        {
            using IApiResponse<SiebelListResponse> response = await _api
                .SearchAsync(
                    bearerToken,
                    _trustedUserName,
                    ServiceRequestMapper.ToSiebel(query),
                    cancellationToken)
                .ConfigureAwait(false);

            // Both documented empty results. The spec declares them, and the 404 is not
            // theoretical: MEASURED against SIT on 2026-09-03, a search that matches
            // nothing answers 404 {"ERROR":"There is no data for the requested resource"}.
            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return ServiceRequestMapper.ToModel((SiebelListResponse?)null);
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);
            return ServiceRequestMapper.ToModel(response.Content);
        }

        /// <inheritdoc/>
        public async Task<ServiceRequest?> GetAsync(
            string bearerToken,
            string serviceRequestKey,
            ServiceRequestReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            using IApiResponse<SiebelServiceRequest> response = await _api
                .GetAsync(
                    bearerToken,
                    _trustedUserName,
                    serviceRequestKey,
                    ServiceRequestMapper.ToSiebel(options),
                    cancellationToken)
                .ConfigureAwait(false);

            // The spec gives this operation both a 204 and a 404 for "there is no such
            // record", and ICM does not say which of "absent" or "not yours" it means.
            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return null;
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            // Missing has its own status codes, handled above. A 200 is specified to carry
            // the record, so an empty body is ICM contradicting itself — same class of
            // problem as a create that returns nothing, and reported the same way rather
            // than being folded into "not found".
            return response.Content is { } record
                ? ServiceRequestMapper.ToModel(record)
                : throw new IcmResponseException(
                    "ICM answered the read with success but returned no record.");
        }

        /// <inheritdoc/>
        public async Task<ServiceRequest> CreateAsync(
            string bearerToken,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);

            using IApiResponse<SiebelWriteResponse> response = await _api
                .CreateAsync(
                    bearerToken,
                    _trustedUserName,
                    ServiceRequestMapper.ToSiebel(input),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            // Unlike an update, a create has no "nothing changed" outcome: if ICM reports
            // success it made a record, and a body that does not contain it means the two
            // ends disagree about what just happened.
            return response.Content?.Items is { } created
                ? ServiceRequestMapper.ToModel(created)
                : throw new IcmResponseException(
                    "ICM reported the service request was created but returned no record.");
        }

        /// <inheritdoc/>
        public async Task<ServiceRequest?> UpdateAsync(
            string bearerToken,
            string serviceRequestKey,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);

            using IApiResponse<SiebelWriteResponse> response = await _api
                .UpdateAsync(
                    bearerToken,
                    _trustedUserName,
                    serviceRequestKey,
                    ServiceRequestMapper.ToSiebel(input),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            return await ReadWriteResultAsync(response).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ServiceRequest?> UpsertAsync(
            string bearerToken,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);

            using IApiResponse<SiebelWriteResponse> response = await _api
                .UpsertAsync(
                    bearerToken,
                    _trustedUserName,
                    ServiceRequestMapper.ToSiebel(input),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            return await ReadWriteResultAsync(response).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(
            string bearerToken,
            string serviceRequestKey,
            CancellationToken cancellationToken = default)
        {
            using IApiResponse<SiebelDeleteResponse> response = await _api
                .DeleteAsync(bearerToken, _trustedUserName, serviceRequestKey, cancellationToken)
                .ConfigureAwait(false);

            // The spec gives the delete all three no-op statuses (204, 304 and 404), and
            // none of them is a failure — there was just nothing to delete.
            if (response.StatusCode is HttpStatusCode.NoContent
                or HttpStatusCode.NotModified
                or HttpStatusCode.NotFound)
            {
                return false;
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Reads a write response, treating <c>304</c>, <c>204</c> and <c>404</c> — all of
        /// which the spec documents on the PUTs — as "nothing to report" rather than as
        /// failures.
        /// </summary>
        private static async Task<ServiceRequest?> ReadWriteResultAsync(
            IApiResponse<SiebelWriteResponse> response)
        {
            if (response.StatusCode is HttpStatusCode.NotModified
                or HttpStatusCode.NoContent
                or HttpStatusCode.NotFound)
            {
                return null;
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            // Past the no-op statuses, a successful write is specified to return the
            // stored record. An empty body is not "nothing changed" — ICM has a status
            // for that — it is a response the caller cannot act on.
            return response.Content?.Items is { } written
                ? ServiceRequestMapper.ToModel(written)
                : throw new IcmResponseException(
                    "ICM reported the write succeeded but returned no record.");
        }
    }
}
