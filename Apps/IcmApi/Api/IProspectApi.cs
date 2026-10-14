namespace Icm.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Refit;

    /// <summary>
    /// The ICM (Siebel) prospect API: <c>data/Prospects/SRProspects</c>, as described by
    /// <c>docs/integration/SRProspects_OpenApi.json</c>, plus the same business component
    /// reached as a service request's <c>SRProspects</c> child collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Internal</b>, and everything <see cref="IServiceRequestApi"/> says about why —
    /// the token and the trusted user name travelling per call, <see cref="IApiResponse{T}"/>
    /// because the status code is the only thing separating "found nothing" from a failure,
    /// the trailing slash — applies here unchanged.
    /// </para>
    /// <para>
    /// <b>Two paths to one component.</b> The document describes the standalone path,
    /// <c>Prospects/SRProspects</c>, with the usual six operations. The rows the bus pass
    /// workflow files are read from the child path,
    /// <c>ServiceRequest/ServiceRequest/{key}/SRProspects</c>, which no document describes.
    /// Both are declared for reading; creating goes through the standalone path only, as a
    /// PUT (see <see cref="UpsertAsync"/>).
    /// </para>
    /// <para>
    /// <b>No update by key, no delete.</b> The document describes both on a person's
    /// application row; MySS has no business rewriting or removing what a citizen
    /// submitted, and an operation that does not exist cannot be called by mistake. The
    /// one PUT declared is the create.
    /// </para>
    /// </remarks>
    [Headers("Accept: application/json")]
    internal interface IProspectApi
    {
        /// <summary>Searches prospects on the standalone path.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="query">The search, paging and field-selection parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the matching page, or <c>204</c>/<c>404</c> when nothing matched.</returns>
        [Get("/data/Prospects/SRProspects/")]
        Task<IApiResponse<SiebelProspectListResponse>> SearchAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Gets one prospect by key on the standalone path.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="prospectKey">The Siebel row id of the prospect (the <c>srprospects_key</c> path segment).</param>
        /// <param name="query">Field selection, child links and visibility mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the row, or <c>204</c>/<c>404</c> when the key matches nothing the caller can see.</returns>
        [Get("/data/Prospects/SRProspects/{srprospects_key}/")]
        Task<IApiResponse<SiebelProspect>> GetAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("srprospects_key")] string prospectKey,
            [Query] SiebelItemQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the prospects on a service request — its <c>SRProspects</c> child collection.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="query">Paging and visibility mode; there is nothing to search on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the rows, or <c>204</c>/<c>404</c> when there are none the caller can see.</returns>
        [Get("/data/ServiceRequest/ServiceRequest/{servicerequest_key}/SRProspects/")]
        Task<IApiResponse<SiebelProspectListResponse>> GetForServiceRequestAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("servicerequest_key")] string serviceRequestKey,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a prospect: a <b>PUT</b> (Siebel upsert) on the standalone path, the body
        /// naming its service request and carrying <c>"Id": "NULL"</c> as the user key.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="prospect">The row to create. Unset properties are omitted from the body rather than sent as null.</param>
        /// <param name="excludeEmptyFieldsInResponse">When true, empty fields are omitted from the returned row.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the created row, including the id Siebel assigned.</returns>
        /// <remarks>
        /// ICM's own REST Data API guidance ("Adding Prospects to a Service Request", ADO
        /// 8249) shows this body; the verb it does not show is PUT. MEASURED 2026-09-29: the
        /// same body by POST is "Invalid input" (<c>SBL-EAI-50243</c>) on icmdev and SIT2
        /// alike, and by PUT creates the row (icmdev <c>1-5M16MO8</c> on SR <c>1-5D7C2PD</c>).
        /// A POST to the child path <c>…/ServiceRequest/{key}/SRProspects/</c> is worse than
        /// useless — Siebel reads the body as a new service request — so no such method exists.
        /// </remarks>
        [Put("/data/Prospects/SRProspects/")]
        Task<IApiResponse<SiebelProspectWriteResponse>> UpsertAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [Body] SiebelProspect prospect,
            [AliasAs("excludeEmptyFieldsInResponse")] bool? excludeEmptyFieldsInResponse = null,
            CancellationToken cancellationToken = default);
    }
}
