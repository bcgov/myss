namespace Icm.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Refit;

    /// <summary>
    /// The ICM (Siebel) Case API: <c>data/Cases/Case</c>, as described by
    /// <c>docs/integration/Case_OpenApi.json</c>, plus the case's <c>Contact</c> child
    /// collection, which no document describes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Internal</b>, and everything <see cref="IServiceRequestApi"/> says about why —
    /// the token and the trusted user name travelling per call, <see cref="IApiResponse{T}"/>
    /// because the status code is the only thing separating "found nothing" from a failure,
    /// the trailing slash — applies here unchanged.
    /// </para>
    /// <para>
    /// <b>Reads only.</b> The document describes the same six operations the service
    /// request has, including a PUT and a DELETE on a person's case. None of those is
    /// declared: MySS reads cases and has no business writing them, and an operation
    /// that does not exist cannot be called by mistake.
    /// </para>
    /// <para>
    /// <b>The path is <c>Cases/Case</c>, not what ICM says it is.</b> The business object's
    /// own describe (<c>data/Cases/describe</c>) names one component,
    /// <c>ICM REST HLS Case</c>, and that name answers <c>SBL-EAI-50257</c>; <c>Case</c> is
    /// the name that works (MEASURED 2026-09-24).
    /// </para>
    /// </remarks>
    [Headers("Accept: application/json")]
    internal interface ICaseApi
    {
        /// <summary>Searches cases.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="query">The search, paging and field-selection parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the matching page, or <c>204</c>/<c>404</c> when nothing matched.</returns>
        [Get("/data/Cases/Case/")]
        Task<IApiResponse<SiebelCaseListResponse>> SearchAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Gets one case by key.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="caseKey">The Siebel row id of the case (the <c>case_key</c> path segment).</param>
        /// <param name="query">Field selection, child links and visibility mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the record, or <c>204</c>/<c>404</c> when the key matches nothing the caller can see.</returns>
        [Get("/data/Cases/Case/{case_key}/")]
        Task<IApiResponse<SiebelCase>> GetAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("case_key")] string caseKey,
            [Query] SiebelItemQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the people on a case — its <c>Contact</c> child collection.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">The ICM user the call acts as, or null to send no header.</param>
        /// <param name="caseKey">The Siebel row id of the case.</param>
        /// <param name="query">Field selection and visibility mode; there is nothing to search on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the rows, or <c>204</c>/<c>404</c> when there are none the caller can see.</returns>
        [Get("/data/Cases/Case/{case_key}/Contact/")]
        Task<IApiResponse<SiebelCaseContactListResponse>> GetContactsAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("case_key")] string caseKey,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);
    }
}
