namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Data;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Attachments module service: validate -> insert row (quarantined) ->
    /// scan -> store -> release. The row goes in before the scan, so a crash
    /// leaves a quarantined row we can find later instead of an orphaned
    /// object in the bucket. A file the scan flags keeps its row as an audit
    /// record; its content never reaches the store.
    /// </summary>
    public partial class AttachmentsService : IAttachmentsService
    {
        private readonly ILogger<AttachmentsService> _logger;
        private readonly AttachmentsDbContext _dbContext;
        private readonly IVirusScanProvider _virusScanProvider;
        private readonly IFileStorageProvider _fileStorageProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly AttachmentsConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttachmentsService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="dbContext">Injected attachments db context.</param>
        /// <param name="virusScanProvider">Injected virus scan provider.</param>
        /// <param name="fileStorageProvider">Injected file storage provider.</param>
        /// <param name="currentUserAccessor">Injected caller accessor.</param>
        /// <param name="config">Injected attachment acceptance rules.</param>
        public AttachmentsService(
            ILogger<AttachmentsService> logger,
            AttachmentsDbContext dbContext,
            IVirusScanProvider virusScanProvider,
            IFileStorageProvider fileStorageProvider,
            ICurrentUserAccessor currentUserAccessor,
            IOptions<AttachmentsConfig> config)
        {
            _logger = logger;
            _dbContext = dbContext;
            _virusScanProvider = virusScanProvider;
            _fileStorageProvider = fileStorageProvider;
            _currentUserAccessor = currentUserAccessor;
            _config = config.Value;
        }

        /// <inheritdoc/>
        public async Task<AttachmentUploadResult> UploadAsync(
            string fileName,
            string contentType,
            long sizeBytes,
            Stream content,
            CancellationToken cancellationToken)
        {
            if (sizeBytes <= 0)
            {
                return AttachmentUploadResult.Rejected(
                    AttachmentRejectionReason.Empty, "The file is empty.");
            }

            if (sizeBytes > _config.MaxSizeBytes)
            {
                return AttachmentUploadResult.Rejected(
                    AttachmentRejectionReason.TooLarge,
                    $"The file exceeds the maximum size of {_config.MaxSizeBytes} bytes.");
            }

            if (!_config.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return AttachmentUploadResult.Rejected(
                    AttachmentRejectionReason.TypeNotAllowed,
                    $"Content type '{contentType}' is not accepted. Accepted: {string.Join(", ", _config.AllowedContentTypes)}.");
            }

            // Buffer the file once so we sniff, scan and store the same bytes.
            // Size is already capped above.
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);

            // Check the magic bytes; the browser's Content-Type alone is not
            // to be trusted (forms architecture, Part 4.13).
            if (!MatchesSignature(contentType, buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
            {
                return AttachmentUploadResult.Rejected(
                    AttachmentRejectionReason.TypeNotAllowed,
                    $"The file content does not match the declared type '{contentType}'.");
            }

            // Write the row first; it stays quarantined until the file is
            // scanned clean and actually stored.
            var attachment = new Attachment
            {
                Id = Guid.NewGuid(),
                OwnerSubject = _currentUserAccessor.User.Subject,
                FileName = TrimFileName(fileName),
                ContentType = contentType,
                SizeBytes = buffer.Length,
                StorageKey = string.Empty,
                Status = AttachmentStatus.Quarantined,
                UploadedAt = DateTimeOffset.UtcNow,
            };
            attachment.StorageKey = BuildStorageKey(attachment.OwnerSubject, attachment.Id, fileName);
            _dbContext.Attachments.Add(attachment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // If the scanner is down this throws and the row stays
            // quarantined, which is the state we want: no verdict yet.
            buffer.Position = 0;
            VirusScanResult verdict = await _virusScanProvider.ScanAsync(buffer, cancellationToken);
            if (!verdict.IsClean)
            {
                attachment.Status = AttachmentStatus.Rejected;
                attachment.ScanSignature = verdict.Signature;
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Don't log the filename; it may contain PII.
                _logger.LogWarning(
                    "Rejected infected attachment {AttachmentId} for {OwnerSubject}: {Signature}",
                    attachment.Id,
                    attachment.OwnerSubject,
                    verdict.Signature);
                return AttachmentUploadResult.Rejected(
                    AttachmentRejectionReason.Infected,
                    $"The file was flagged by the virus scan ({verdict.Signature}).");
            }

            buffer.Position = 0;
            attachment.ETag = await _fileStorageProvider.PutAsync(
                attachment.StorageKey, buffer, contentType, cancellationToken);
            attachment.Status = AttachmentStatus.Released;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Released attachment {AttachmentId} ({SizeBytes} bytes, {ContentType}) for {OwnerSubject}",
                attachment.Id,
                attachment.SizeBytes,
                attachment.ContentType,
                attachment.OwnerSubject);

            return AttachmentUploadResult.Accepted(ToResponse(attachment));
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<AttachmentResponseModel>> ListOwnAsync(CancellationToken cancellationToken)
        {
            string subject = _currentUserAccessor.User.Subject;
            return await _dbContext.Attachments
                .AsNoTracking()
                .Where(a => a.OwnerSubject == subject && a.Status == AttachmentStatus.Released)
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => new AttachmentResponseModel
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    SizeBytes = a.SizeBytes,
                    Status = a.Status,
                    SubmissionId = a.SubmissionId,
                    UploadedAt = a.UploadedAt,
                })
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Magic-byte check for the declared content type. A type with no
        /// check here is rejected, not waved through.
        /// </summary>
        private static bool MatchesSignature(string contentType, ReadOnlySpan<byte> head)
        {
            return contentType.ToLowerInvariant() switch
            {
                "application/pdf" => head.Length >= 5 && head[..5].SequenceEqual("%PDF-"u8),
                "image/png" => head.Length >= 8
                    && head[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                "image/jpeg" => head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
                _ => false,
            };
        }

        /// <summary>
        /// Object key = owner prefix + attachment id + original extension (if
        /// it's a plain one). The filename itself never ends up in the key.
        /// </summary>
        private static string BuildStorageKey(string ownerSubject, Guid id, string fileName)
        {
            string owner = UnsafeKeyCharacters().Replace(ownerSubject, "-");
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!SafeExtension().IsMatch(extension))
            {
                extension = string.Empty;
            }

            return $"{owner}/{id}{extension}";
        }

        private static string TrimFileName(string fileName)
        {
            // Drop any path that came with the filename and fit it to the
            // column.
            string name = Path.GetFileName(fileName);
            return name.Length <= 255 ? name : name[^255..];
        }

        private static AttachmentResponseModel ToResponse(Attachment attachment)
        {
            return new AttachmentResponseModel
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.SizeBytes,
                Status = attachment.Status,
                SubmissionId = attachment.SubmissionId,
                UploadedAt = attachment.UploadedAt,
            };
        }

        [GeneratedRegex("[^A-Za-z0-9@._-]")]
        private static partial Regex UnsafeKeyCharacters();

        [GeneratedRegex(@"^\.[a-z0-9]{1,10}$")]
        private static partial Regex SafeExtension();
    }
}
