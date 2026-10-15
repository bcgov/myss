namespace Icm.Api.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Refit;

    /// <summary><see cref="IProspectRepository"/> over ICM's Siebel REST API.</summary>
    /// <remarks>
    /// Thin by design, like <see cref="CaseRepository"/>: call, check the status, map. The
    /// ICM user every call acts as is fixed for the lifetime of the repository, for the
    /// reason <see cref="ServiceRequestRepository"/> gives.
    /// </remarks>
    public class ProspectRepository : IProspectRepository
    {
        /// <summary>
        /// The most pages a service request's prospect read will follow before deciding
        /// ICM is not going to say "last". A bus pass request has one or two rows; the cap
        /// turns an ICM that ignores <c>StartRowNum</c> into an exception rather than a loop.
        /// </summary>
        internal const int MaxChildPages = 50;

        private readonly IProspectApi _api;
        private readonly string? _trustedUserName;

        /// <summary>Initializes a new instance of the <see cref="ProspectRepository"/> class.</summary>
        /// <param name="httpClient">
        /// The client to send on. Its <see cref="HttpClient.BaseAddress"/> must be the ICM
        /// base URL including the version prefix, e.g.
        /// <c>https://icmsit2.api.gov.bc.ca/gov/v1.0</c>.
        /// </param>
        /// <param name="trustedUserName">
        /// The ICM user every call through this repository acts as, sent as
        /// <c>X-ICM-TrustedUserName</c>. Null sends no header.
        /// </param>
        public ProspectRepository(HttpClient httpClient, string? trustedUserName = null)
            : this(RestService.For<IProspectApi>(httpClient, IcmRefitSettings.Create()), trustedUserName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProspectRepository"/> class over a
        /// specific transport. For tests.
        /// </summary>
        /// <param name="api">The transport.</param>
        /// <param name="trustedUserName">The ICM user calls act as.</param>
        internal ProspectRepository(IProspectApi api, string? trustedUserName = null)
        {
            _api = api;
            _trustedUserName = trustedUserName;
        }

        /// <inheritdoc/>
        public async Task<ProspectPage> SearchAsync(
            string bearerToken,
            ProspectQuery query,
            CancellationToken cancellationToken = default)
        {
            // Built before the call so a refused query never reaches the network.
            SiebelListQuery siebelQuery = ProspectMapper.ToSiebel(query);

            using IApiResponse<SiebelProspectListResponse> response = await _api
                .SearchAsync(bearerToken, _trustedUserName, siebelQuery, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return ProspectMapper.ToModel((SiebelProspectListResponse?)null);
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            return response.Content is { Items: not null } body
                ? ProspectMapper.ToModel(body, ReadTotalCount(response))
                : throw new IcmResponseException(
                    "ICM answered the prospect search with success but returned no rows.");
        }

        /// <inheritdoc/>
        public async Task<Prospect?> GetAsync(
            string bearerToken,
            string prospectKey,
            ProspectReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prospectKey);

            using IApiResponse<SiebelProspect> response = await _api
                .GetAsync(bearerToken, _trustedUserName, prospectKey, ProspectMapper.ToReadSiebel(options), cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return null;
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            return response.Content is { } row
                ? ProspectMapper.ToModel(row)
                : throw new IcmResponseException(
                    "ICM answered the prospect read with success but returned no row.");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Paged exactly as <see cref="CaseRepository.GetContactsAsync"/> pages a case's
        /// contacts: <see cref="ProspectMapper.ChildPageSize"/> rows at a time until ICM's
        /// <c>lastpage</c> says so, stopping on an empty page or a <c>404</c>/<c>204</c>
        /// past the end, and giving up after <see cref="MaxChildPages"/>. MEASURED
        /// 2026-09-29: every bus pass SR read answered <c>"lastpage": "true"</c> on its
        /// only page.
        /// </remarks>
        public async Task<IReadOnlyList<Prospect>> GetForServiceRequestAsync(
            string bearerToken,
            string serviceRequestKey,
            ProspectReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestKey);

            List<Prospect> rows = [];
            for (int page = 0; page < MaxChildPages; page++)
            {
                using IApiResponse<SiebelProspectListResponse> response = await _api
                    .GetForServiceRequestAsync(
                        bearerToken,
                        _trustedUserName,
                        serviceRequestKey,
                        ProspectMapper.ToChildSiebel(options, startRowNum: rows.Count),
                        cancellationToken)
                    .ConfigureAwait(false);

                // First page: no such SR, or no rows the caller can see (the 404 is what
                // ICM sends for an SR with no prospects — MEASURED 2026-09-25 on the
                // Error - Web SRs' other children). Later page: the rows ran out.
                if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
                {
                    return rows;
                }

                await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

                if (response.Content is not { Items: not null } body)
                {
                    throw new IcmResponseException(
                        "ICM answered the service request's prospect read with success but returned no rows.");
                }

                rows.AddRange(ProspectMapper.ToModels(body));
                if (body.IsLastPage || body.Items.Count == 0)
                {
                    return rows;
                }
            }

            throw new IcmResponseException(
                $"ICM kept answering the service request's prospect read without a last page after {MaxChildPages} pages.");
        }

        /// <inheritdoc/>
        public async Task<Prospect> CreateAsync(
            string bearerToken,
            ProspectInput input,
            CancellationToken cancellationToken = default)
        {
            SiebelProspect row = ProspectMapper.ToSiebel(input);

            // A PUT, not a POST: the create is a Siebel upsert keyed by the placeholder Id
            // (MEASURED 2026-09-29; the same body by POST is refused).
            using IApiResponse<SiebelProspectWriteResponse> response = await _api
                .UpsertAsync(bearerToken, _trustedUserName, row, null, cancellationToken)
                .ConfigureAwait(false);

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            // A create has no "nothing changed" outcome: if ICM reports success it made a
            // row, and a body without it means the two ends disagree about what happened.
            return response.Content?.Items is { } created
                ? ProspectMapper.ToModel(created)
                : throw new IcmResponseException(
                    "ICM reported the prospect was created but returned no row.");
        }

        private static long? ReadTotalCount(IApiResponse<SiebelProspectListResponse> response) =>
            response.Headers is { } headers
                && headers.TryGetValues("Total-Record-Count", out IEnumerable<string>? values)
                && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long count)
                ? count
                : null;
    }
}
