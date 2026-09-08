namespace Icm.Api.Workflows
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Workflows.Contracts;
    using Refit;

    /// <summary>
    /// The ICM (Siebel) bus pass workflow:
    /// <c>workflow/ICM Receive Bus Pass Online Request Wrapper WF</c>, as described by
    /// <c>docs/integration/BusPassWorkflow_OpenApi.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why <c>Workflows/</c> and not <c>Api/</c>.</b> The two folders are peers with
    /// one distinction: <c>Api/</c> is direct REST over a business component — the caller
    /// names the record and the fields, and Siebel does exactly that. A workflow endpoint
    /// invokes a Siebel workflow process that calls other services behind it — here,
    /// matching or creating the contact, creating the service request, and filing the
    /// request under a transaction. The caller sends a message and gets an outcome, not a
    /// record.
    /// </para>
    /// <para>
    /// <b>Internal</b>, like <see cref="IServiceRequestApi"/> and for the same reason:
    /// <see cref="Repositories.IBusPassRepository"/> is the published boundary.
    /// </para>
    /// <para>
    /// The path segment is the workflow's display name, spaces and all, percent-encoded in
    /// the attribute because a URI template cannot carry a literal space. The trailing
    /// slash is the spec's and is kept — Siebel is sensitive to it.
    /// </para>
    /// </remarks>
    [Headers("Accept: application/json")]
    internal interface IBusPassWorkflowApi
    {
        /// <summary>
        /// The header naming the ICM user a call acts as — the same header, with the same
        /// meaning, as on the data APIs.
        /// </summary>
        public const string TrustedUserNameHeader = IServiceRequestApi.TrustedUserNameHeader;

        /// <summary>
        /// Submits a bus pass request to the receiving workflow.
        /// </summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="trustedUserName">
        /// The ICM user the call acts as, or null to send no header.
        /// </param>
        /// <param name="envelope">The message to deliver.</param>
        /// <param name="excludeEmptyFieldsInResponse">
        /// When true, empty fields are omitted from the returned out-args.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>200</c> with the workflow's out-args.</returns>
        [Post("/workflow/ICM%20Receive%20Bus%20Pass%20Online%20Request%20Wrapper%20WF/")]
        Task<IApiResponse<SiebelBusPassResponse>> SubmitAsync(
            [Authorize("Bearer")] string bearerToken,
            [Header(TrustedUserNameHeader)] string? trustedUserName,
            [Body] SiebelBusPassEnvelope envelope,
            [AliasAs("excludeEmptyFieldsInResponse")] bool? excludeEmptyFieldsInResponse = null,
            CancellationToken cancellationToken = default);
    }
}
