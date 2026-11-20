namespace Myss.AdminApi.Providers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Common.Storage;

    /// <summary>
    /// Stands in for the object store when ObjectStorage is not configured,
    /// so the admin API still starts and only document downloads fail.
    /// </summary>
    public sealed class UnconfiguredFileStorageProvider : IFileStorageProvider
    {
        private const string Message =
            "ObjectStorage is not configured (ServiceUrl, Bucket, AccessKey and SecretKey are all required). "
            + "Deployed: set ObjectStorage__ServiceUrl, ObjectStorage__Bucket, ObjectStorage__AccessKey and ObjectStorage__SecretKey from the same secret as MyssApi.";

        /// <inheritdoc/>
        public Task<string?> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);

        /// <inheritdoc/>
        public Task<Stream?> GetAsync(string key, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);

        /// <inheritdoc/>
        public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);
    }
}
