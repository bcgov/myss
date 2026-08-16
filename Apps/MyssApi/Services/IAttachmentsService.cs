namespace Myss.Api.Services
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// The attachments module service: validates, scans and stores files for
    /// the authenticated caller.
    /// </summary>
    public interface IAttachmentsService
    {
        /// <summary>
        /// Validates, virus-scans and stores a file for the current user.
        /// </summary>
        /// <param name="fileName">The original filename, for display.</param>
        /// <param name="contentType">The declared content type.</param>
        /// <param name="sizeBytes">The content size in bytes.</param>
        /// <param name="content">The file content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored attachment, or the rejection reason.</returns>
        /// <exception cref="Providers.VirusScanUnavailableException">
        /// Thrown when no scan verdict could be obtained; the file is not
        /// stored, and its row stays quarantined for reconciliation.
        /// </exception>
        Task<AttachmentUploadResult> UploadAsync(
            string fileName,
            string contentType,
            long sizeBytes,
            Stream content,
            CancellationToken cancellationToken);

        /// <summary>
        /// Lists the current user's released attachments, newest first.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<IReadOnlyList<AttachmentResponseModel>> ListOwnAsync(CancellationToken cancellationToken);
    }
}
