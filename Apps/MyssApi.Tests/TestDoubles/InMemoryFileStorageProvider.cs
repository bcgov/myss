namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Common.Storage;
    using System.Collections.Concurrent;
    using System.Security.Cryptography;

    /// <summary>
    /// In-memory <see cref="IFileStorageProvider"/> for tests. The ETag is a
    /// hash of the content, so tests can check it round-trips onto the row.
    /// </summary>
    public sealed class InMemoryFileStorageProvider : IFileStorageProvider
    {
        private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType, string ETag)> _objects = new();

        /// <summary>
        /// Gets the stored objects, keyed by storage key.
        /// </summary>
        public IReadOnlyDictionary<string, (byte[] Content, string ContentType, string ETag)> Objects => _objects;

        /// <inheritdoc/>
        public async Task<string?> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            byte[] bytes = buffer.ToArray();
            string etag = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            _objects[key] = (bytes, contentType, etag);
            return etag;
        }

        /// <inheritdoc/>
        public Task<Stream?> GetAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream?>(
                _objects.TryGetValue(key, out var stored) ? new MemoryStream(stored.Content, writable: false) : null);
        }

        /// <inheritdoc/>
        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            _objects.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }
}
