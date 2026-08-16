namespace Myss.Api.Providers
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
    using Myss.Api.Configuration.Models;

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
        public void Dispose()
        {
            _client.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
