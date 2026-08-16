namespace Myss.Api.Providers
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stores accepted upload content in the object store.
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
    }
}
