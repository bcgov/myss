namespace Myss.Api.Services
{
    using System;
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

        /// <summary>
        /// Finds which of the given attachments the owner uploaded and were
        /// released after a clean scan.
        /// </summary>
        /// <param name="ids">The attachment IDs to look for.</param>
        /// <param name="ownerSubject">The identity subject that must own them.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The IDs, among those given, that the owner uploaded and that are released.</returns>
        Task<IReadOnlySet<Guid>> FindReleasedAsync(
            IReadOnlyCollection<Guid> ids,
            string ownerSubject,
            CancellationToken cancellationToken);
    }
}
