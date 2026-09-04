namespace Icm.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Refit;

    /// <summary>
    /// The ICM (Siebel) Service Request API:
    /// <c>data/ServiceRequest/ServiceRequest</c>, as described by
    /// <c>docs/integration/SR_OpenApi.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Internal.</b> <see cref="Repositories.IServiceRequestRepository"/> is the
    /// published boundary; this interface and the contracts it speaks are Siebel's shape
    /// and stay inside the assembly, so no caller can reach past the mapping and the
    /// status-code handling that make those results usable.
    /// </para>
    /// <para>
    /// <b>The bearer token is a parameter, not ambient state.</b> Every method takes the
    /// caller's token and Refit's <see cref="AuthorizeAttribute"/> turns it into
    /// <c>Authorization: Bearer {token}</c>. ICM applies the calling user's Siebel
    /// visibility to every read and write, so a client that carried one token for the
    /// process would be answering as the wrong person — the token has to travel with the
    /// call. A <see cref="System.Net.Http.DelegatingHandler"/> that injects a service
    /// account token would defeat this; if one is ever added it must be for a genuinely
    /// unattended caller, and it must not silently override a token passed here.
    /// </para>
    /// <para>
    /// <b>Two things identify a call, not one.</b> The bearer token says which application
    /// is calling; <c>X-ICM-TrustedUserName</c> says which ICM user it is acting as, and
    /// ICM applies that user's Siebel visibility. Both travel per call for the same reason.
    /// A null user name sends no header at all — Refit omits a header whose value is null.
    /// </para>
    /// <para>
    /// <b>Every method returns <see cref="IApiResponse{T}"/> rather than the payload.</b>
    /// Siebel answers "found nothing" with <c>204</c> on some operations and <c>404</c> on
    /// others (the spec describes both, on all six), and the status code is the only thing
    /// that distinguishes them from a real failure. Keeping it means the repository can
    /// turn "nothing found" into a null or an empty page and everything else into an
    /// <see cref="ApiException"/> — a decision this layer is not in a position to make.
    /// </para>
    /// <para>
    /// Paths keep the trailing slash the spec declares; Siebel is sensitive to it. The base
    /// address supplies the host and the <c>/gov/v1.0</c> prefix.
    /// </para>
    /// </remarks>
    [Headers("Accept: application/json")]
    internal interface IServiceRequestApi
    {
        /// <summary>
        /// The header naming the ICM user a call acts as. Fixed by ICM, so it is a constant
        /// rather than configuration — only its value varies.
        /// </summary>
        public const string TrustedUserNameHeader = "X-ICM-TrustedUserName";

        /// <summary>
        /// Searches service requests.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">
        /// The ICM user the call acts as, or null to send no header.
        /// </param>
        /// <param name="query">
        /// The search, paging and field-selection parameters. Note that Siebel's default
        /// <c>ViewMode</c> restricts the result to the caller's own records.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// <c>200</c> with the matching page, or <c>204</c> when nothing matched.
        /// </returns>
        [Get("/data/ServiceRequest/ServiceRequest/")]
        Task<IApiResponse<SiebelListResponse>> SearchAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(TrustedUserNameHeader)] string? trustedUserName,
            [Query] SiebelListQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a service request.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">
        /// The ICM user the call acts as, or null to send no header.
        /// </param>
        /// <param name="serviceRequest">
        /// The record to create. Read-only fields are ignored by Siebel; unset properties
        /// are omitted from the body rather than sent as null.
        /// </param>
        /// <param name="excludeEmptyFieldsInResponse">
        /// When true, empty fields are omitted from the returned record.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the created record, including the id Siebel assigned.</returns>
        [Post("/data/ServiceRequest/ServiceRequest/")]
        Task<IApiResponse<SiebelWriteResponse>> CreateAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(TrustedUserNameHeader)] string? trustedUserName,
            [Body] SiebelServiceRequest serviceRequest,
            [AliasAs("excludeEmptyFieldsInResponse")] bool? excludeEmptyFieldsInResponse = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts or updates a service request without naming a key in the URL — Siebel
        /// matches an existing record on the business component's user keys and creates one
        /// when it finds none.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">
        /// The ICM user the call acts as, or null to send no header.
        /// </param>
        /// <param name="serviceRequest">The record to upsert.</param>
        /// <param name="excludeEmptyFieldsInResponse">
        /// When true, empty fields are omitted from the returned record.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// <c>200</c> with the stored record, or <c>304</c> when the record was unchanged.
        /// </returns>
        /// <remarks>
        /// Prefer <see cref="UpdateAsync"/> when the record's key is known: which record an
        /// upsert lands on depends on Siebel's user-key configuration rather than on
        /// anything visible in this call.
        /// </remarks>
        [Put("/data/ServiceRequest/ServiceRequest/")]
        Task<IApiResponse<SiebelWriteResponse>> UpsertAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(TrustedUserNameHeader)] string? trustedUserName,
            [Body] SiebelServiceRequest serviceRequest,
            [AliasAs("excludeEmptyFieldsInResponse")] bool? excludeEmptyFieldsInResponse = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets one service request by key.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">
        /// The ICM user the call acts as, or null to send no header.
        /// </param>
        /// <param name="serviceRequestKey">
        /// The Siebel row id of the service request (the <c>servicerequest_key</c> path
        /// segment).
        /// </param>
        /// <param name="query">Field selection, child links and visibility mode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// <c>200</c> with the record, or <c>204</c>/<c>404</c> when the key matches nothing
        /// the caller can see.
        /// </returns>
        [Get("/data/ServiceRequest/ServiceRequest/{servicerequest_key}/")]
        Task<IApiResponse<SiebelServiceRequest>> GetAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("servicerequest_key")] string serviceRequestKey,
            [Query] SiebelItemQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the service request identified by key.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">
        /// The ICM user the call acts as, or null to send no header.
        /// </param>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="serviceRequest">
        /// The fields to change. Only the properties that are set are sent, so this is a
        /// partial update — sending a whole record read back from
        /// <see cref="GetAsync"/> would rewrite every field.
        /// </param>
        /// <param name="excludeEmptyFieldsInResponse">
        /// When true, empty fields are omitted from the returned record.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// <c>200</c> with the stored record, or <c>304</c> when the record was unchanged.
        /// </returns>
        [Put("/data/ServiceRequest/ServiceRequest/{servicerequest_key}/")]
        Task<IApiResponse<SiebelWriteResponse>> UpdateAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("servicerequest_key")] string serviceRequestKey,
            [Body] SiebelServiceRequest serviceRequest,
            [AliasAs("excludeEmptyFieldsInResponse")] bool? excludeEmptyFieldsInResponse = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the service request identified by key.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">
        /// The ICM user the call acts as, or null to send no header.
        /// </param>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with an empty body, or <c>204</c> when there was nothing to delete.</returns>
        [Delete("/data/ServiceRequest/ServiceRequest/{servicerequest_key}/")]
        Task<IApiResponse<SiebelDeleteResponse>> DeleteAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(TrustedUserNameHeader)] string? trustedUserName,
            [AliasAs("servicerequest_key")] string serviceRequestKey,
            CancellationToken cancellationToken = default);
    }
}
