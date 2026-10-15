namespace Icm.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Refit;

    /// <summary>
    /// The ICM (Siebel) activity APIs: <c>data/Activities/Activities</c> and
    /// <c>data/ActivityPlan/ActivityPlan</c>, as described by
    /// <c>docs/integration/Activities_OpenApi.json</c> and <c>ActivityPlan_OpenApi.json</c>,
    /// plus both as a service request's child collections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Internal</b>, on the terms <see cref="IServiceRequestApi"/> sets out. Activities
    /// are read on either path and created on the standalone one — a PUT keyed by
    /// <c>"Id": "NULL"</c>, the shape ICM's REST API guidance prescribes ("GET-PUT-POST
    /// Activities", ADO 8646; MEASURED SIT2 2026-09-29). Plans are read only: the
    /// write-side <c>Template</c> picklist refuses the bus pass template
    /// (<c>SBL-EAI-04401</c>, MEASURED), so no plan write is declared.
    /// </para>
    /// <para>
    /// No update by key and no delete: an activity is a worker's record, and MySS has no
    /// business rewriting one.
    /// </para>
    /// </remarks>
    [Headers("Accept: application/json")]
    internal interface IActivityApi
    {
        /// <summary>Searches activities on the standalone path.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="query">The search, paging and visibility parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the matching page, or <c>204</c>/<c>404</c> when nothing matched.</returns>
        [Get("/data/Activities/Activities/")]
        Task<IApiResponse<SiebelActivityListResponse>> SearchAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Gets one activity by key.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="activityKey">The Siebel row id of the activity.</param>
        /// <param name="query">Child links and visibility mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the row, or <c>204</c>/<c>404</c> when the key matches nothing the caller can see.</returns>
        [Get("/data/Activities/Activities/{activities_key}/")]
        Task<IApiResponse<SiebelActivity>> GetAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("activities_key")] string activityKey,
            [Query] SiebelItemQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the activities on a service request — its <c>Activities</c> child collection.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="query">Paging and visibility mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the rows, or <c>204</c>/<c>404</c> when there are none the caller can see.</returns>
        [Get("/data/ServiceRequest/ServiceRequest/{servicerequest_key}/Activities/")]
        Task<IApiResponse<SiebelActivityListResponse>> GetForServiceRequestAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("servicerequest_key")] string serviceRequestKey,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Creates an activity: a <b>PUT</b> (Siebel upsert) keyed by the placeholder <c>Id</c>.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="activity">The row to create. Unset properties are omitted from the body.</param>
        /// <param name="excludeEmptyFieldsInResponse">When true, empty fields are omitted from the returned row.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the created row, including the id Siebel assigned.</returns>
        [Put("/data/Activities/Activities/")]
        Task<IApiResponse<SiebelActivityWriteResponse>> UpsertAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [Body] SiebelActivity activity,
            [AliasAs("excludeEmptyFieldsInResponse")] bool? excludeEmptyFieldsInResponse = null,
            CancellationToken cancellationToken = default);

        /// <summary>Searches activity plans on the standalone path.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="query">The search, paging and visibility parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the matching page, or <c>204</c>/<c>404</c> when nothing matched.</returns>
        [Get("/data/ActivityPlan/ActivityPlan/")]
        Task<IApiResponse<SiebelActivityPlanListResponse>> SearchPlansAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the activity plans on a service request — its <c>ActivityPlan</c> child collection.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="query">Paging and visibility mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the rows, or <c>204</c>/<c>404</c> when there are none the caller can see.</returns>
        [Get("/data/ServiceRequest/ServiceRequest/{servicerequest_key}/ActivityPlan/")]
        Task<IApiResponse<SiebelActivityPlanListResponse>> GetPlansForServiceRequestAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("servicerequest_key")] string serviceRequestKey,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);
    }
}
