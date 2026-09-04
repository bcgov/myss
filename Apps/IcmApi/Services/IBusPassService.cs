namespace Icm.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// Bus pass submissions, authenticated on the caller's behalf.
    /// </summary>
    /// <remarks>
    /// The thing to inject, like <see cref="IServiceRequestService"/>. Reach for
    /// <see cref="Repositories.IBusPassRepository"/> instead only when the caller already
    /// holds a token of its own.
    /// </remarks>
    public interface IBusPassService
    {
        /// <summary>Submits a bus pass request.</summary>
        /// <param name="application">The request as the applicant stated it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The workflow's answer. Check <see cref="BusPassResult.ErrorCode"/> — a business
        /// rejection arrives here, not as an exception.
        /// </returns>
        Task<BusPassResult> SubmitAsync(
            BusPassApplication application,
            CancellationToken cancellationToken = default);
    }
}
