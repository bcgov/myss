namespace Icm.Api.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Refit;

    /// <summary>
    /// <see cref="IContactRepository"/> over ICM's Siebel REST API.
    /// </summary>
    /// <remarks>
    /// Thin by design, like <see cref="ServiceRequestRepository"/>: call, check the status,
    /// map. The ICM user every call acts as is fixed for the lifetime of the repository,
    /// for the reason given there.
    /// </remarks>
    public class ContactRepository : IContactRepository
    {
        private readonly IContactApi _api;
        private readonly string? _trustedUserName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactRepository"/> class.
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
        public ContactRepository(HttpClient httpClient, string? trustedUserName = null)
            : this(RestService.For<IContactApi>(httpClient, IcmRefitSettings.Create()), trustedUserName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactRepository"/> class over a
        /// specific transport. For tests.
        /// </summary>
        /// <param name="api">The transport.</param>
        /// <param name="trustedUserName">The ICM user calls act as.</param>
        internal ContactRepository(IContactApi api, string? trustedUserName = null)
        {
            _api = api;
            _trustedUserName = trustedUserName;
        }

        /// <inheritdoc/>
        public async Task<ContactPage> SearchAsync(
            string bearerToken,
            ContactQuery query,
            CancellationToken cancellationToken = default)
        {
            // Built before the call so a refused query never reaches the network.
            SiebelListQuery siebelQuery = ContactMapper.ToSiebel(query);

            using IApiResponse<SiebelContactListResponse> response = await _api
                .SearchAsync(bearerToken, _trustedUserName, siebelQuery, cancellationToken)
                .ConfigureAwait(false);

            // The document declares both for "nothing matched". The 404 is the one ICM
            // uses: MEASURED against SIT1 on 2026-09-17, a search matching no contact
            // answers 404 {"ERROR":"There is no data for the requested resource"}.
            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return ContactMapper.ToModel((SiebelContactListResponse?)null);
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            // "No such contact" has its own statuses, handled above, so a 200 without
            // records is ICM contradicting itself. Reading that as "not found" would turn
            // an upstream fault into a citizen being told they have no file.
            return response.Content is { Items: not null } body
                ? ContactMapper.ToModel(body, ReadTotalCount(response))
                : throw new IcmResponseException(
                    "ICM answered the contact search with success but returned no records.");
        }

        /// <summary>
        /// Reads ICM's <c>Total-Record-Count</c> header, sent when the search asked for a
        /// count. Null when absent or unreadable — an extra the caller opted into, not
        /// something worth failing a successful search over.
        /// </summary>
        private static long? ReadTotalCount(IApiResponse<SiebelContactListResponse> response) =>
            response.Headers is { } headers
                && headers.TryGetValues("Total-Record-Count", out IEnumerable<string>? values)
                && long.TryParse(
                    values?.FirstOrDefault(),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out long count)
                ? count
                : null;
    }
}
