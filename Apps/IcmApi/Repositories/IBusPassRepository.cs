namespace Icm.Api.Repositories
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// Submits bus pass requests to ICM's receiving workflow, given a token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The workflow counterpart of <see cref="IServiceRequestRepository"/>: same
    /// boundary, same rules about tokens and status codes, but the operation is a
    /// business submission rather than record access — ICM's workflow matches or creates
    /// the contact and files the service request itself.
    /// </para>
    /// <para>
    /// <b>A returned result is not necessarily a success.</b> The workflow reports
    /// business rejections inside a 200 — check <see cref="BusPassResult.ErrorCode"/>.
    /// An HTTP-level failure still throws <see cref="Refit.ApiException"/>.
    /// </para>
    /// </remarks>
    public interface IBusPassRepository
    {
        /// <summary>Submits a bus pass request.</summary>
        /// <param name="bearerToken">The caller's access token.</param>
        /// <param name="application">The request as the applicant stated it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The workflow's answer. Check <see cref="BusPassResult.ErrorCode"/> — a business
        /// rejection arrives here, not as an exception.
        /// </returns>
        Task<BusPassResult> SubmitAsync(
            string bearerToken,
            BusPassApplication application,
            CancellationToken cancellationToken = default);
    }
}
