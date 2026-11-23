namespace Myss.AdminApi.Intake
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Common.Auth;
    using Myss.Common.Storage;
    using Myss.Data.Attachments;
    using Myss.Data.Intake;
    using Myss.Data.Platform;

    /// <summary>
    /// A supporting document as a worker sees it on the review screen.
    /// </summary>
    public class ReviewAttachmentModel
    {
        /// <summary>Gets or sets the attachment identifier.</summary>
        public required Guid Id { get; set; }

        /// <summary>Gets or sets the original filename, for display.</summary>
        public required string FileName { get; set; }

        /// <summary>Gets or sets the content type.</summary>
        public required string ContentType { get; set; }

        /// <summary>Gets or sets the content size in bytes.</summary>
        public required long SizeBytes { get; set; }

        /// <summary>Gets or sets when the applicant added it.</summary>
        public required DateTimeOffset UploadedAt { get; set; }
    }

    /// <summary>
    /// One supporting document opened for download.
    /// </summary>
    /// <param name="FileName">The original filename.</param>
    /// <param name="ContentType">The content type.</param>
    /// <param name="Content">The content; the caller disposes it.</param>
    public sealed record ReviewAttachmentContent(string FileName, string ContentType, Stream Content);

    /// <summary>
    /// The supporting documents an applicant added to a submitted
    /// application: the list for the review screen, and the file itself.
    /// </summary>
    public interface IReviewAttachmentsService
    {
        /// <summary>
        /// Lists the documents on a submitted application, oldest first.
        /// </summary>
        /// <param name="applicationId">The application.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The documents, or null when the application is not one a worker can see.</returns>
        Task<IReadOnlyList<ReviewAttachmentModel>?> ListAsync(Guid applicationId, CancellationToken cancellationToken);

        /// <summary>
        /// Opens one document on a submitted application.
        /// </summary>
        /// <param name="applicationId">The application.</param>
        /// <param name="attachmentId">The document.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The document, or null when there is no such document a worker can see.</returns>
        Task<ReviewAttachmentContent?> OpenAsync(Guid applicationId, Guid attachmentId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Reads the attachments schema (shared model in MyssData, migrated by
    /// MyssApi) and the object store. Read-only: workers never change an
    /// applicant's documents. Only released (scanned clean) files are listed,
    /// and only on applications that have been submitted and are in a form
    /// the review queue takes, so a draft's files stay private to the
    /// applicant.
    /// </summary>
    public class ReviewAttachmentsService : IReviewAttachmentsService
    {
        private readonly ILogger<ReviewAttachmentsService> _logger;
        private readonly IntakeDbContext _intakeDbContext;
        private readonly AttachmentsDbContext _attachmentsDbContext;
        private readonly IEventStore _eventStore;
        private readonly IFileStorageProvider _fileStorageProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewAttachmentsService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="intakeDbContext">Injected intake context.</param>
        /// <param name="attachmentsDbContext">Injected attachments context.</param>
        /// <param name="eventStore">Injected event store.</param>
        /// <param name="fileStorageProvider">Injected object store.</param>
        /// <param name="currentUserAccessor">Injected caller accessor.</param>
        public ReviewAttachmentsService(
            ILogger<ReviewAttachmentsService> logger,
            IntakeDbContext intakeDbContext,
            AttachmentsDbContext attachmentsDbContext,
            IEventStore eventStore,
            IFileStorageProvider fileStorageProvider,
            ICurrentUserAccessor currentUserAccessor)
        {
            _logger = logger;
            _intakeDbContext = intakeDbContext;
            _attachmentsDbContext = attachmentsDbContext;
            _eventStore = eventStore;
            _fileStorageProvider = fileStorageProvider;
            _currentUserAccessor = currentUserAccessor;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewAttachmentModel>?> ListAsync(
            Guid applicationId,
            CancellationToken cancellationToken)
        {
            (ApplicationAnswers Row, ApplicationState State)? found = await FindSubmittedAsync(applicationId, cancellationToken);
            if (found is null)
            {
                return null;
            }

            List<Attachment> attachments = await Released(found.Value.Row, found.Value.State)
                .OrderBy(a => a.UploadedAt)
                .ToListAsync(cancellationToken);
            return attachments.Select(a => new ReviewAttachmentModel
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                UploadedAt = a.UploadedAt,
            }).ToList();
        }

        /// <inheritdoc/>
        public async Task<ReviewAttachmentContent?> OpenAsync(
            Guid applicationId,
            Guid attachmentId,
            CancellationToken cancellationToken)
        {
            (ApplicationAnswers Row, ApplicationState State)? found = await FindSubmittedAsync(applicationId, cancellationToken);
            if (found is null)
            {
                return null;
            }

            Attachment? attachment = await Released(found.Value.Row, found.Value.State)
                .SingleOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
            if (attachment is null)
            {
                return null;
            }

            Stream? content = await _fileStorageProvider.GetAsync(attachment.StorageKey, cancellationToken);
            if (content is null)
            {
                _logger.LogError(
                    "Attachment {AttachmentId} on application {ApplicationId} is released but its content is missing",
                    attachment.Id,
                    applicationId);
                return null;
            }

            // Who opened which applicant document is worth an audit line.
            _logger.LogInformation(
                "Worker {Idir} opened attachment {AttachmentId} on application {ApplicationId}",
                _currentUserAccessor.User.IdirUsername,
                attachment.Id,
                applicationId);
            return new ReviewAttachmentContent(attachment.FileName, attachment.ContentType, content);
        }

        private IQueryable<Attachment> Released(ApplicationAnswers row, ApplicationState state)
        {
            // The owner check keeps a file someone else linked to this id
            // (they cannot: the citizen API checks ownership) out regardless.
            IQueryable<Attachment> query = _attachmentsDbContext.Attachments
                .AsNoTracking()
                .Where(a => a.SubmissionId == row.Id
                    && a.OwnerSubject == row.OwnerSubject
                    && a.Status == AttachmentStatus.Released);

            // Only the files the latest submit recorded. Events written before
            // documents were recorded have no list; those show every file.
            IReadOnlyList<Guid>? recorded = state.Submitted?.AttachmentIds;
            return recorded is null ? query : query.Where(a => recorded.Contains(a.Id));
        }

        private async Task<(ApplicationAnswers Row, ApplicationState State)?> FindSubmittedAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            ApplicationAnswers? row = await _intakeDbContext.Applications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    a => a.Id == id && ReviewService.QueuedForms.Contains(a.FormSpecId),
                    cancellationToken);
            if (row is null)
            {
                return null;
            }

            ApplicationState state = ApplicationProjection.Fold(await _eventStore.LoadAsync(id, cancellationToken));
            return state.SubmittedAt is null ? null : (row, state);
        }
    }
}
