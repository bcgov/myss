namespace Myss.AdminApi.Tests.TestDoubles
{
    using System.Collections.Concurrent;
    using Myss.Common.Storage;

    /// <summary>
    /// In-memory object store, shared by the citizen and staff hosts so a file
    /// a citizen uploads is the file a worker downloads.
    /// </summary>
    public sealed class InMemoryFileStorageProvider : IFileStorageProvider
    {
        private readonly ConcurrentDictionary<string, byte[]> _objects = new();

        /// <inheritdoc/>
        public async Task<string?> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _objects[key] = buffer.ToArray();
            return key;
        }

        /// <inheritdoc/>
        public Task<Stream?> GetAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(_objects.TryGetValue(key, out byte[]? bytes) ? new MemoryStream(bytes, writable: false) : null);

        /// <inheritdoc/>
        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            _objects.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }
}
