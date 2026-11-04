namespace Myss.AdminApi.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;

    /// <summary>
    /// The confirmation message editor: read a form's live message and its history,
    /// and save a new one.
    /// </summary>
    public interface ISubmissionAcknowledgementService
    {
        /// <summary>Gets a form's live message and its history, uncached.</summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The message and its history.</returns>
        /// <exception cref="ContentEngineUnavailableException">The content engine could not be read.</exception>
        Task<SubmissionAcknowledgementAdminModel> GetAsync(string formSpecId, CancellationToken cancellationToken);

        /// <summary>
        /// Validates and saves a form's new message, which applicants see once MyssApi's
        /// cache expires.
        /// </summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="request">The texts, and the entry the edit started from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The saved message, the reasons it was refused, or that the live message changed.</returns>
        /// <exception cref="StrapiWriteException">The content engine refused the write.</exception>
        /// <exception cref="ContentEngineUnavailableException">The content engine could not be read or written.</exception>
        Task<SubmissionAcknowledgementSaveResultModel> SaveAsync(
            string formSpecId,
            SaveSubmissionAcknowledgementRequestModel request,
            CancellationToken cancellationToken);
    }
}
