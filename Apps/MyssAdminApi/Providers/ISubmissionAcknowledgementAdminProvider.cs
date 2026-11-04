namespace Myss.AdminApi.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Common.Content;

    /// <summary>
    /// Reads and creates a form's confirmation message entries in the content engine,
    /// for the editor. Entries are only ever added, never changed.
    /// </summary>
    public interface ISubmissionAcknowledgementAdminProvider
    {
        /// <summary>Reads a form's newest published entries, uncached.</summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="count">The most entries to read.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The entries, newest first; the first is the live message.</returns>
        /// <exception cref="ContentEngineUnavailableException">The content engine could not be read.</exception>
        Task<IReadOnlyList<SubmissionAcknowledgementEntry>> ReadNewestAsync(
            string formSpecId,
            int count,
            CancellationToken cancellationToken);

        /// <summary>Creates and publishes a form's new entry, which becomes the live message.</summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="texts">The page heading, alert title and message, already validated.</param>
        /// <param name="changedBy">The IDIR username of whoever saved it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored entry.</returns>
        /// <exception cref="StrapiWriteException">The content engine refused the write.</exception>
        /// <exception cref="ContentEngineUnavailableException">
        /// The content engine could not be reached, did not answer in time, or answered with something unreadable.
        /// </exception>
        Task<SubmissionAcknowledgementEntry> CreateAsync(
            string formSpecId,
            SubmissionAcknowledgementTexts texts,
            string changedBy,
            CancellationToken cancellationToken);
    }

    /// <summary>The three texts of a confirmation message.</summary>
    /// <param name="Heading">The page heading.</param>
    /// <param name="Title">The alert title.</param>
    /// <param name="Body">The message.</param>
    public sealed record SubmissionAcknowledgementTexts(string Heading, string Title, string Body);
}
