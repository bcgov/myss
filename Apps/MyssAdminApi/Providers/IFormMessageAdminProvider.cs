namespace Myss.AdminApi.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Common.Content;

    /// <summary>
    /// Reads and creates form message entries in the content engine, for the editor.
    /// Entries are only ever added, never changed.
    /// </summary>
    public interface IFormMessageAdminProvider
    {
        /// <summary>Reads a form message's newest published entries, uncached.</summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="messageKey">The message's key within the form.</param>
        /// <param name="count">The most entries to read.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The entries, newest first; the first is the live message.</returns>
        /// <exception cref="ContentEngineUnavailableException">The content engine could not be read.</exception>
        Task<IReadOnlyList<FormMessageEntry>> ReadNewestAsync(
            string formSpecId,
            string messageKey,
            int count,
            CancellationToken cancellationToken);

        /// <summary>Creates and publishes a form message's new entry, which becomes the live message.</summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="messageKey">The message's key within the form.</param>
        /// <param name="texts">The page heading, title and message, already validated.</param>
        /// <param name="changedBy">The IDIR username of whoever saved it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored entry.</returns>
        /// <exception cref="StrapiWriteException">The content engine refused the write.</exception>
        /// <exception cref="ContentEngineUnavailableException">
        /// The content engine could not be reached, did not answer in time, or answered with something unreadable.
        /// </exception>
        Task<FormMessageEntry> CreateAsync(
            string formSpecId,
            string messageKey,
            FormMessageTexts texts,
            string changedBy,
            CancellationToken cancellationToken);
    }

    /// <summary>The three texts of a form message.</summary>
    /// <param name="Heading">The page heading.</param>
    /// <param name="Title">The title.</param>
    /// <param name="Body">The message.</param>
    public sealed record FormMessageTexts(string Heading, string Title, string Body);
}
