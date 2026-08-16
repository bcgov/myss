namespace Myss.Api.Providers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Scans content for malware before it is accepted into storage.
    /// </summary>
    public interface IVirusScanProvider
    {
        /// <summary>
        /// Scans a stream from its current position to the end.
        /// </summary>
        /// <param name="content">The content to scan.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The scan verdict.</returns>
        /// <exception cref="VirusScanUnavailableException">
        /// Thrown when the scanner can't be reached or gives no verdict.
        /// Callers fail closed on this: no verdict, no upload.
        /// </exception>
        Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The verdict of a virus scan.
    /// </summary>
    /// <param name="IsClean">Whether the content passed the scan.</param>
    /// <param name="Signature">The matched signature name when infected.</param>
    public sealed record VirusScanResult(bool IsClean, string? Signature = null);

    /// <summary>
    /// Raised when no scan verdict could be obtained.
    /// </summary>
    public class VirusScanUnavailableException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="VirusScanUnavailableException"/> class.</summary>
        /// <param name="message">The failure description.</param>
        /// <param name="innerException">The underlying failure, if any.</param>
        public VirusScanUnavailableException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
