namespace Myss.Common.Storage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Stores files in an S3-compatible bucket. Explicit service URL plus
    /// path-style addressing, so MinIO locally and the platform's endpoint
    /// deployed are just config changes.
    /// </summary>
    public class S3FileStorageProvider : IFileStorageProvider, IDisposable
    {
        private readonly ILogger<S3FileStorageProvider> _logger;
        private readonly ObjectStorageConfig _config;
        private readonly IAmazonS3 _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="S3FileStorageProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="config">Injected object storage settings.</param>
        public S3FileStorageProvider(
            ILogger<S3FileStorageProvider> logger,
            IOptions<ObjectStorageConfig> config)
        {
            _logger = logger;
            _config = config.Value;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = _config.ServiceUrl,
                ForcePathStyle = _config.ForcePathStyle,
                AuthenticationRegion = _config.Region,
            };
            _client = new AmazonS3Client(
                new BasicAWSCredentials(_config.AccessKey, _config.SecretKey),
                s3Config);
        }

        /// <inheritdoc/>
        public async Task<string?> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            var request = new PutObjectRequest
            {
                BucketName = _config.Bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            };

            PutObjectResponse response = await _client.PutObjectAsync(request, cancellationToken);
            _logger.LogInformation(
                "Stored attachment {StorageKey} in bucket {Bucket}", key, _config.Bucket);
            return response.ETag?.Trim('"');
        }

        /// <inheritdoc/>
        public async Task<Stream?> GetAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                GetObjectResponse response = await _client.GetObjectAsync(_config.Bucket, key, cancellationToken);
                return new ResponseOwningStream(response);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("No object {StorageKey} in bucket {Bucket}", key, _config.Bucket);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            await _client.DeleteObjectAsync(_config.Bucket, key, cancellationToken);
            _logger.LogInformation("Removed attachment {StorageKey} from bucket {Bucket}", key, _config.Bucket);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// The response stream of a GetObject call that also disposes the
    /// response (which holds the HTTP connection) when it is disposed.
    /// </summary>
    internal sealed class ResponseOwningStream : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _inner;

        public ResponseOwningStream(GetObjectResponse response)
        {
            _response = response;
            _inner = response.ResponseStream;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _response.ContentLength;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
