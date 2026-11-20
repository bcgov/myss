namespace Myss.Common.Storage
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stores, reads and removes accepted upload content in the object store.
    /// MyssApi writes (uploads) and removes (an applicant deleting a file from
    /// a draft); MyssAdminApi only reads, for the worker download.
    /// </summary>
    public interface IFileStorageProvider
    {
        /// <summary>
        /// Stores a stream, from its current position to the end, under the
        /// given key.
        /// </summary>
        /// <param name="key">The generated storage key (never a user-supplied filename).</param>
        /// <param name="content">The content to store.</param>
        /// <param name="contentType">The declared content type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The ETag the store reported for the object, when it reported one.</returns>
        Task<string?> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

        /// <summary>
        /// Opens a stored object for reading. The caller disposes the stream.
        /// </summary>
        /// <param name="key">The storage key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The content, or null when no object has that key.</returns>
        Task<Stream?> GetAsync(string key, CancellationToken cancellationToken);

        /// <summary>
        /// Removes a stored object. Removing a key that does not exist is not
        /// an error.
        /// </summary>
        /// <param name="key">The storage key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes once the object is gone.</returns>
        Task DeleteAsync(string key, CancellationToken cancellationToken);
    }
}
