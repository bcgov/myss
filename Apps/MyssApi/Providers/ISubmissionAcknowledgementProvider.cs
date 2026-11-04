namespace Myss.Api.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// Provides the confirmation message a form's page shows after a successful
    /// submission, from the content engine.
    /// </summary>
    public interface ISubmissionAcknowledgementProvider
    {
        /// <summary>Gets a form's live confirmation message.</summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The message, or none, or unavailable; never null.</returns>
        Task<SubmissionAcknowledgementLookup> GetLiveAsync(string formSpecId, CancellationToken cancellationToken);
    }
}
