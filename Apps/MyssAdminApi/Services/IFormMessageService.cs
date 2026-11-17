namespace Myss.AdminApi.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.Common.Content;

    /// <summary>
    /// The form message editor: list the defined messages, read one message's live
    /// texts and history, and save a new version of it.
    /// </summary>
    public interface IFormMessageService
    {
        /// <summary>Lists every defined message with its last change, uncached.</summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>One summary per definition, in the order the definitions list them.</returns>
        /// <exception cref="ContentEngineUnavailableException">The content engine could not be read.</exception>
        Task<IReadOnlyList<FormMessageSummaryModel>> ListAsync(CancellationToken cancellationToken);

        /// <summary>Gets a message's live texts and its history, uncached.</summary>
        /// <param name="definition">The message.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The message and its history.</returns>
        /// <exception cref="ContentEngineUnavailableException">The content engine could not be read.</exception>
        Task<FormMessageAdminModel> GetAsync(FormMessageDefinition definition, CancellationToken cancellationToken);

        /// <summary>
        /// Validates and saves a message's new texts, which applicants see once MyssApi's
        /// cache expires.
        /// </summary>
        /// <param name="definition">The message.</param>
        /// <param name="request">The texts, and the entry the edit started from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The saved message, the reasons it was refused, or that the live message changed.</returns>
        /// <exception cref="StrapiWriteException">The content engine refused the write.</exception>
        /// <exception cref="ContentEngineUnavailableException">The content engine could not be read or written.</exception>
        Task<FormMessageSaveResultModel> SaveAsync(
            FormMessageDefinition definition,
            SaveFormMessageRequestModel request,
            CancellationToken cancellationToken);
    }
}
