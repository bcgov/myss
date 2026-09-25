namespace Icm.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Refit;

    /// <summary>
    /// The ICM (Siebel) Contact API: <c>data/ICMContact/ICMContact</c>, as described by
    /// <c>docs/integration/Contact_OpenApi.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Internal</b>, and everything <see cref="IServiceRequestApi"/> says about why —
    /// the token and the trusted user name travelling per call, <see cref="IApiResponse{T}"/>
    /// because the status code is the only thing separating "found nothing" from a failure,
    /// the trailing slash — applies here unchanged.
    /// </para>
    /// <para>
    /// <b>The search only.</b> The document describes the same six operations the service
    /// request has, including a PUT and a DELETE on a person's record. None of those is
    /// declared: MySS looks contacts up and has no business writing them, and an operation
    /// that does not exist cannot be called by mistake.
    /// </para>
    /// </remarks>
    [Headers("Accept: application/json")]
    internal interface IContactApi
    {
        /// <summary>Searches contacts.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">
        /// The ICM user the call acts as, or null to send no header.
        /// </param>
        /// <param name="query">
        /// The search, paging and field-selection parameters — the same query string the
        /// service request search takes; the document declares an identical parameter list.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// <c>200</c> with the matching page, or <c>204</c>/<c>404</c> when nothing matched.
        /// </returns>
        [Get("/data/ICMContact/ICMContact/")]
        Task<IApiResponse<SiebelContactListResponse>> SearchAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(IServiceRequestApi.TrustedUserNameHeader)] string? trustedUserName,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);
    }
}
