namespace Myss.Api.Configuration.Models
{
    /// <summary>
    /// Connection settings for the S3-compatible object store, bound from the
    /// <c>ObjectStorage</c> section. MinIO locally, whatever S3-compliant
    /// endpoint the platform provides when deployed. Required: the app refuses
    /// to start without it (see Startup).
    /// </summary>
    public class ObjectStorageConfig
    {
        /// <summary>
        /// Gets or sets the endpoint URL (e.g. <c>http://localhost:9000</c>).
        /// </summary>
        public string? ServiceUrl { get; set; }

        /// <summary>
        /// Gets or sets the bucket uploads are stored in.
        /// </summary>
        public string? Bucket { get; set; }

        /// <summary>
        /// Gets or sets the access key id.
        /// </summary>
        public string? AccessKey { get; set; }

        /// <summary>
        /// Gets or sets the secret access key. Comes from a secret-backed env
        /// var when deployed, never from a committed file.
        /// </summary>
        public string? SecretKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use path-style addressing
        /// (<c>endpoint/bucket/key</c>). MinIO and most non-AWS stores need it,
        /// hence the default.
        /// </summary>
        public bool ForcePathStyle { get; set; } = true;

        /// <summary>
        /// Gets or sets the region. Non-AWS endpoints mostly ignore it, but the
        /// SDK wants one for request signing.
        /// </summary>
        public string Region { get; set; } = "us-east-1";

        /// <summary>
        /// Gets a value indicating whether all four required values are set.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ServiceUrl)
            && !string.IsNullOrWhiteSpace(Bucket)
            && !string.IsNullOrWhiteSpace(AccessKey)
            && !string.IsNullOrWhiteSpace(SecretKey);
    }
}
