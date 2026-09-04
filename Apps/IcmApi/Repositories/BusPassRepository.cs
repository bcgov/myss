namespace Icm.Api.Repositories
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;
    using Icm.Api.Workflows;
    using Icm.Api.Workflows.Contracts;
    using Refit;

    /// <summary>
    /// <see cref="IBusPassRepository"/> over ICM's workflow REST API.
    /// </summary>
    /// <remarks>
    /// Thin like <see cref="ServiceRequestRepository"/>: map, call, check the status. The
    /// only extra collaborator is a <see cref="TimeProvider"/>, because the message header
    /// carries a timestamp and a hard-coded clock would make the mapper untestable.
    /// </remarks>
    public class BusPassRepository : IBusPassRepository
    {
        private readonly IBusPassWorkflowApi _api;
        private readonly string? _trustedUserName;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassRepository"/> class.
        /// </summary>
        /// <param name="httpClient">
        /// The client to send on. Its <see cref="HttpClient.BaseAddress"/> must be the ICM
        /// base URL including the version prefix, e.g.
        /// <c>https://icmsit1.api.gov.bc.ca/gov/v1.0</c> — the same base the service
        /// request repository uses.
        /// </param>
        /// <param name="trustedUserName">
        /// The ICM user every call through this repository acts as, sent as
        /// <c>X-ICM-TrustedUserName</c>. Null sends no header.
        /// </param>
        /// <param name="timeProvider">
        /// The clock stamped into the message header; null uses the system clock.
        /// </param>
        /// <remarks>
        /// Takes an <see cref="HttpClient"/> rather than the Refit interface because that
        /// interface is internal — register this with <c>IHttpClientFactory</c> like the
        /// service request repository.
        /// </remarks>
        public BusPassRepository(
            HttpClient httpClient,
            string? trustedUserName = null,
            TimeProvider? timeProvider = null)
            : this(
                RestService.For<IBusPassWorkflowApi>(httpClient, IcmRefitSettings.Create()),
                trustedUserName,
                timeProvider)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassRepository"/> class over a
        /// specific transport. For tests.
        /// </summary>
        /// <param name="api">The transport.</param>
        /// <param name="trustedUserName">The ICM user calls act as.</param>
        /// <param name="timeProvider">The clock stamped into the message header.</param>
        internal BusPassRepository(
            IBusPassWorkflowApi api,
            string? trustedUserName = null,
            TimeProvider? timeProvider = null)
        {
            _api = api;
            _trustedUserName = trustedUserName;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <inheritdoc/>
        public async Task<BusPassResult> SubmitAsync(
            string bearerToken,
            BusPassApplication application,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(application);

            using IApiResponse<SiebelBusPassResponse> response = await _api
                .SubmitAsync(
                    bearerToken,
                    _trustedUserName,
                    BusPassMapper.ToSiebel(application, _timeProvider.GetUtcNow()),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            // A submission has no "found nothing" outcome: success with no out-args means
            // the two ends disagree about what just happened, which is the same class of
            // problem as a create that returns no record.
            return response.Content is { } body
                ? BusPassMapper.ToModel(body)
                : throw new IcmResponseException(
                    "ICM reported the bus pass submission succeeded but returned no result.");
        }
    }
}
