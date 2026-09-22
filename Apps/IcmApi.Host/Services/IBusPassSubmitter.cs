namespace Icm.Api.Host.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// Submits a bus pass request to ICM. The host's boundary to the client
    /// library, so endpoint tests can substitute a fake.
    /// </summary>
    public interface IBusPassSubmitter
    {
        /// <summary>
        /// Submits the request.
        /// </summary>
        /// <param name="application">The request as the applicant stated it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The workflow's answer. A business rejection is an ordinary result carrying
        /// an error code, not an exception.
        /// </returns>
        /// <exception cref="IcmUpstreamException">No outcome could be obtained from ICM.</exception>
        Task<BusPassResult> SubmitAsync(BusPassApplication application, CancellationToken cancellationToken);
    }
}
