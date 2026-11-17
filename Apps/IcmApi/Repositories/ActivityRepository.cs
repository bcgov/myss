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

    /// <summary><see cref="IActivityRepository"/> over ICM's Siebel REST API.</summary>
    /// <remarks>Thin by design, like <see cref="ProspectRepository"/>: call, check the status, map.</remarks>
    public class ActivityRepository : IActivityRepository
    {
        /// <summary>The most pages a child read follows before deciding ICM is not going to say "last".</summary>
        internal const int MaxChildPages = 50;

        private readonly IActivityApi _api;
        private readonly string? _trustedUserName;

        /// <summary>Initializes a new instance of the <see cref="ActivityRepository"/> class.</summary>
        /// <param name="httpClient">The client to send on; its base address is the ICM base URL including the version prefix.</param>
        /// <param name="trustedUserName">The ICM user every call acts as, sent as <c>X-ICM-TrustedUserName</c>. Null sends no header.</param>
        public ActivityRepository(HttpClient httpClient, string? trustedUserName = null)
            : this(RestService.For<IActivityApi>(httpClient, IcmRefitSettings.Create()), trustedUserName)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ActivityRepository"/> class over a specific transport. For tests.</summary>
        /// <param name="api">The transport.</param>
        /// <param name="trustedUserName">The ICM user calls act as.</param>
        internal ActivityRepository(IActivityApi api, string? trustedUserName = null)
        {
            _api = api;
            _trustedUserName = trustedUserName;
        }

        /// <inheritdoc/>
        public async Task<ActivityPage> SearchAsync(string bearerToken, ActivityQuery query, CancellationToken cancellationToken = default)
        {
            SiebelListQuery siebelQuery = ActivityMapper.ToSiebel(query);

            using IApiResponse<SiebelActivityListResponse> response = await _api
                .SearchAsync(bearerToken, _trustedUserName, siebelQuery, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return ActivityMapper.ToModel((SiebelActivityListResponse?)null);
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            return response.Content is { Items: not null } body
                ? ActivityMapper.ToModel(body, ReadTotalCount(response.Headers))
                : throw new IcmResponseException("ICM answered the activity search with success but returned no rows.");
        }

        /// <inheritdoc/>
        public async Task<Activity?> GetAsync(
            string bearerToken, string activityKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(activityKey);

            using IApiResponse<SiebelActivity> response = await _api
                .GetAsync(bearerToken, _trustedUserName, activityKey, ActivityMapper.ToReadSiebel(options), cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return null;
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            return response.Content is { } row
                ? ActivityMapper.ToModel(row)
                : throw new IcmResponseException("ICM answered the activity read with success but returned no row.");
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Activity>> GetForServiceRequestAsync(
            string bearerToken, string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestKey);

            List<Activity> rows = [];
            for (int page = 0; page < MaxChildPages; page++)
            {
                using IApiResponse<SiebelActivityListResponse> response = await _api
                    .GetForServiceRequestAsync(
                        bearerToken, _trustedUserName, serviceRequestKey, ActivityMapper.ToChildSiebel(options, rows.Count), cancellationToken)
                    .ConfigureAwait(false);

                // The 404 is what an SR with no activities answers (MEASURED 2026-09-25 and
                // 2026-09-29 on the Error - Web and the directly created SRs).
                if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
                {
                    return rows;
                }

                await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

                if (response.Content is not { Items: not null } body)
                {
                    throw new IcmResponseException("ICM answered the service request's activity read with success but returned no rows.");
                }

                rows.AddRange(ActivityMapper.ToModels(body));
                if (body.IsLastPage || body.Items.Count == 0)
                {
                    return rows;
                }
            }

            throw new IcmResponseException(
                $"ICM kept answering the service request's activity read without a last page after {MaxChildPages} pages.");
        }

        /// <inheritdoc/>
        public async Task<Activity> CreateAsync(string bearerToken, ActivityInput input, CancellationToken cancellationToken = default)
        {
            SiebelActivity row = ActivityMapper.ToSiebel(input);

            using IApiResponse<SiebelActivityWriteResponse> response = await _api
                .UpsertAsync(bearerToken, _trustedUserName, row, null, cancellationToken)
                .ConfigureAwait(false);

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            return response.Content?.Items is { } created
                ? ActivityMapper.ToModel(created)
                : throw new IcmResponseException("ICM reported the activity was created but returned no row.");
        }

        /// <inheritdoc/>
        public async Task<ActivityPlanPage> SearchPlansAsync(string bearerToken, ActivityPlanQuery query, CancellationToken cancellationToken = default)
        {
            SiebelListQuery siebelQuery = ActivityMapper.ToSiebel(query);

            using IApiResponse<SiebelActivityPlanListResponse> response = await _api
                .SearchPlansAsync(bearerToken, _trustedUserName, siebelQuery, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return ActivityMapper.ToModel((SiebelActivityPlanListResponse?)null);
            }

            await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

            return response.Content is { Items: not null } body
                ? ActivityMapper.ToModel(body, ReadTotalCount(response.Headers))
                : throw new IcmResponseException("ICM answered the activity plan search with success but returned no rows.");
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ActivityPlan>> GetPlansForServiceRequestAsync(
            string bearerToken, string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestKey);

            List<ActivityPlan> rows = [];
            for (int page = 0; page < MaxChildPages; page++)
            {
                using IApiResponse<SiebelActivityPlanListResponse> response = await _api
                    .GetPlansForServiceRequestAsync(
                        bearerToken, _trustedUserName, serviceRequestKey, ActivityMapper.ToChildSiebel(options, rows.Count), cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
                {
                    return rows;
                }

                await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);

                if (response.Content is not { Items: not null } body)
                {
                    throw new IcmResponseException("ICM answered the service request's activity plan read with success but returned no rows.");
                }

                rows.AddRange(ActivityMapper.ToModels(body));
                if (body.IsLastPage || body.Items.Count == 0)
                {
                    return rows;
                }
            }

            throw new IcmResponseException(
                $"ICM kept answering the service request's activity plan read without a last page after {MaxChildPages} pages.");
        }

        private static long? ReadTotalCount(System.Net.Http.Headers.HttpResponseHeaders? headers) =>
            headers is not null
                && headers.TryGetValues("Total-Record-Count", out IEnumerable<string>? values)
                && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long count)
                ? count
                : null;
    }
}
