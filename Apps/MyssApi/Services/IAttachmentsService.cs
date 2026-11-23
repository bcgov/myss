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
        /// <param name="submissionId">
        /// The application the file is added to, if any. The link is set only
        /// when the file is released, so a quarantined row never belongs to
        /// an application.
        /// </param>
        /// <param name="stillAccepting">
        /// Asked once more after the scan and store, just before release: false
        /// (for example, the application was submitted meanwhile) removes the
        /// stored copy and refuses the upload as <see cref="AttachmentRejectionReason.NoLongerAccepted"/>.
        /// </param>
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
            CancellationToken cancellationToken,
            Guid? submissionId = null,
            Func<CancellationToken, Task<bool>>? stillAccepting = null);

        /// <summary>
        /// Lists the current user's released attachments, newest first.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<IReadOnlyList<AttachmentResponseModel>> ListOwnAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Lists the caller's released attachments linked to one application
        /// (or other submission), oldest first, the order they were added in.
        /// </summary>
        /// <param name="submissionId">The application the files belong to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The attachments.</returns>
        Task<IReadOnlyList<AttachmentResponseModel>> ListForSubmissionAsync(
            Guid submissionId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Removes one of the caller's attachments from an application: the
        /// stored content is deleted and the row is dropped. The caller checks
        /// first that the application may still be changed.
        /// </summary>
        /// <param name="submissionId">The application the file belongs to.</param>
        /// <param name="attachmentId">The attachment.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>False when the caller has no such attachment on that application.</returns>
        Task<bool> DeleteFromSubmissionAsync(
            Guid submissionId,
            Guid attachmentId,
            CancellationToken cancellationToken);

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
