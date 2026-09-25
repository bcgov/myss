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

    /// <summary><see cref="ICaseRepository"/> over ICM's Siebel REST API.</summary>
    /// <remarks>
    /// Thin by design, like <see cref="ContactRepository"/>: call, check the status, map.
    /// The ICM user every call acts as is fixed for the lifetime of the repository, for
    /// the reason <see cref="ServiceRequestRepository"/> gives.
    /// </remarks>
    public class CaseRepository : ICaseRepository
    {
        private readonly ICaseApi _api;
        private readonly string? _trustedUserName;

        /// <summary>Initializes a new instance of the <see cref="CaseRepository"/> class.</summary>
        /// <param name="httpClient">
        /// The client to send on. Its <see cref="HttpClient.BaseAddress"/> must be the ICM
        /// base URL including the version prefix, e.g.
        /// <c>https://icmsit2.api.gov.bc.ca/gov/v1.0</c>.
        /// </param>
        /// <param name="trustedUserName">
        /// The ICM user every call through this repository acts as, sent as
        /// <c>X-ICM-TrustedUserName</c>. Null sends no header.
        /// </param>
        public CaseRepository(HttpClient httpClient, string? trustedUserName = null)
            : this(RestService.For<ICaseApi>(httpClient, IcmRefitSettings.Create()), trustedUserName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CaseRepository"/> class over a
        /// specific transport. For tests.
        /// </summary>
        /// <param name="api">The transport.</param>
        /// <param name="trustedUserName">The ICM user calls act as.</param>
        internal CaseRepository(ICaseApi api, string? trustedUserName = null)
        {
            _api = api;
            _trustedUserName = trustedUserName;
        }

        /// <inheritdoc/>
        public async Task<CasePage> SearchAsync(
            string bearerToken,
            CaseQuery query,
            CancellationToken cancellationToken = default)
        {
            // Built before the call so a refused query never reaches the network.
            SiebelListQuery siebelQuery = CaseMapper.ToSiebel(query);

            using IApiResponse<SiebelCaseListResponse> response = await _api
                .SearchAsync(bearerToken, _trustedUserName, siebelQuery, cancellationToken)
                .ConfigureAwait(false);

            // The 404 is the one ICM uses (MEASURED on SIT2 2026-09-24: a search matching
            // no case, or one the ViewMode hides, answers 404 {"ERROR":"There is no data
            // for the requested resource"}); the 204 is documented.
            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return CaseMapper.ToModel((SiebelCaseListResponse?)null);
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            // "No such case" has its own statuses, handled above, so a 200 without records
            // is ICM contradicting itself, not an empty result.
            return response.Content is { Items: not null } body
                ? CaseMapper.ToModel(body, ReadTotalCount(response))
                : throw new IcmResponseException(
                    "ICM answered the case search with success but returned no records.");
        }

        /// <inheritdoc/>
        public async Task<Case?> GetAsync(
            string bearerToken,
            string caseKey,
            CaseReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caseKey);

            using IApiResponse<SiebelCase> response = await _api
                .GetAsync(bearerToken, _trustedUserName, caseKey, CaseMapper.ToReadSiebel(options), cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return null;
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            return response.Content is { } record
                ? CaseMapper.ToModel(record)
                : throw new IcmResponseException(
                    "ICM answered the case read with success but returned no record.");
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CaseContact>> GetContactsAsync(
            string bearerToken,
            string caseKey,
            CaseReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caseKey);

            using IApiResponse<SiebelCaseContactListResponse> response = await _api
                .GetContactsAsync(
                    bearerToken, _trustedUserName, caseKey, CaseMapper.ToContactsSiebel(options), cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return [];
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            return response.Content is { Items: not null } body
                ? CaseMapper.ToModel(body)
                : throw new IcmResponseException(
                    "ICM answered the case contact read with success but returned no rows.");
        }

        /// <summary>
        /// Reads ICM's <c>Total-Record-Count</c> header, sent when the search asked for a
        /// count. Null when absent or unreadable.
        /// </summary>
        private static long? ReadTotalCount(IApiResponse<SiebelCaseListResponse> response) =>
            response.Headers is { } headers
                && headers.TryGetValues("Total-Record-Count", out IEnumerable<string>? values)
                && long.TryParse(values?.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long count)
                ? count
                : null;
    }
}
