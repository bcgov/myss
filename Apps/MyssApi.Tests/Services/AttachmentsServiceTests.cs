namespace Myss.Api.Tests.Services
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Data;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// Tests for <see cref="AttachmentsService"/>: the quarantined ->
    /// released/rejected lifecycle, that nothing unscanned reaches the store,
    /// and that reads only return the caller's released rows.
    /// </summary>
    public class AttachmentsServiceTests
    {
        private static readonly byte[] PdfBytes = "%PDF-1.7 test"u8.ToArray();

        private readonly FakeVirusScanProvider _scanner = new();
        private readonly InMemoryFileStorageProvider _storage = new();

        [Fact]
        public async Task Upload_CleanFile_IsReleasedWithTheStoresETag()
        {
            using AttachmentsDbContext db = NewDb();
            AttachmentsService service = NewService(db, subject: "user-1");

            AttachmentUploadResult result = await service.UploadAsync(
                "statement.pdf", "application/pdf", PdfBytes.Length, new MemoryStream(PdfBytes), CancellationToken.None);

            Assert.Null(result.Rejection);
            Assert.NotNull(result.Attachment);
            Assert.Equal("statement.pdf", result.Attachment.FileName);
            Assert.Equal(AttachmentStatus.Released, result.Attachment.Status);

            Attachment row = Assert.Single(await db.Attachments.ToListAsync());
            Assert.Equal("user-1", row.OwnerSubject);
            Assert.Equal(AttachmentStatus.Released, row.Status);

            // Same bytes were scanned and stored, and the store's ETag ended
            // up on the row.
            Assert.Equal(PdfBytes, Assert.Single(_scanner.ScannedPayloads));
            Assert.Equal(PdfBytes, _storage.Objects[row.StorageKey].Content);
            Assert.Equal(_storage.Objects[row.StorageKey].ETag, row.ETag);
        }

        [Fact]
        public async Task Upload_StorageKey_IsGeneratedNotTheFilename()
        {
            using AttachmentsDbContext db = NewDb();
            AttachmentsService service = NewService(db, subject: "guid-123@bceidbasic");

            AttachmentUploadResult result = await service.UploadAsync(
                "../../etc/passwd my statement.pdf",
                "application/pdf",
                PdfBytes.Length,
                new MemoryStream(PdfBytes),
                CancellationToken.None);

            Attachment row = Assert.Single(await db.Attachments.ToListAsync());
            Assert.DoesNotContain("passwd", row.StorageKey, StringComparison.Ordinal);
            Assert.StartsWith("guid-123@bceidbasic/", row.StorageKey, StringComparison.Ordinal);
            Assert.EndsWith(".pdf", row.StorageKey, StringComparison.Ordinal);

            // The display name keeps only the leaf name.
            Assert.NotNull(result.Attachment);
            Assert.Equal("passwd my statement.pdf", result.Attachment.FileName);
        }

        [Fact]
        public async Task Upload_TypeNotAllowed_IsRejectedWithoutScanRowOrStore()
        {
            using AttachmentsDbContext db = NewDb();
            AttachmentsService service = NewService(db);

            AttachmentUploadResult result = await service.UploadAsync(
                "run.exe", "application/x-msdownload", 4, new MemoryStream([1, 2, 3, 4]), CancellationToken.None);

            Assert.Equal(AttachmentRejectionReason.TypeNotAllowed, result.Rejection);
            Assert.Empty(_scanner.ScannedPayloads);
            Assert.Empty(_storage.Objects);
            Assert.Empty(await db.Attachments.ToListAsync());
        }

        [Fact]
        public async Task Upload_BytesNotMatchingDeclaredType_IsRejectedBySniff()
        {
            // Declared as PDF but the bytes have no PDF signature — the
            // declared type alone doesn't get you in.
            using AttachmentsDbContext db = NewDb();
            AttachmentsService service = NewService(db);

            AttachmentUploadResult result = await service.UploadAsync(
                "not-really.pdf", "application/pdf", 4, new MemoryStream([1, 2, 3, 4]), CancellationToken.None);

            Assert.Equal(AttachmentRejectionReason.TypeNotAllowed, result.Rejection);
            Assert.Empty(_scanner.ScannedPayloads);
            Assert.Empty(_storage.Objects);
            Assert.Empty(await db.Attachments.ToListAsync());
        }

        [Fact]
        public async Task Upload_TooLargeOrEmpty_IsRejected()
        {
            using AttachmentsDbContext db = NewDb();
            AttachmentsService service = NewService(db, maxSizeBytes: 10);

            AttachmentUploadResult tooLarge = await service.UploadAsync(
                "big.pdf", "application/pdf", 11, new MemoryStream(new byte[11]), CancellationToken.None);
            AttachmentUploadResult empty = await service.UploadAsync(
                "empty.pdf", "application/pdf", 0, new MemoryStream(), CancellationToken.None);

            Assert.Equal(AttachmentRejectionReason.TooLarge, tooLarge.Rejection);
            Assert.Equal(AttachmentRejectionReason.Empty, empty.Rejection);
            Assert.Empty(_storage.Objects);
            Assert.Empty(await db.Attachments.ToListAsync());
        }

        [Fact]
        public async Task Upload_InfectedFile_KeepsARejectedAuditRowButStoresNothing()
        {
            using AttachmentsDbContext db = NewDb();
            AttachmentsService service = NewService(db);
            _scanner.Result = new VirusScanResult(IsClean: false, "Win.Test.EICAR_HDB-1");

            AttachmentUploadResult result = await service.UploadAsync(
                "totally-a-statement.pdf", "application/pdf", PdfBytes.Length, new MemoryStream(PdfBytes), CancellationToken.None);

            Assert.Equal(AttachmentRejectionReason.Infected, result.Rejection);
            Assert.Contains("Win.Test.EICAR_HDB-1", result.Detail, StringComparison.Ordinal);
            Assert.Empty(_storage.Objects);

            // The audit trail: a rejected row that names the signature.
            Attachment row = Assert.Single(await db.Attachments.ToListAsync());
            Assert.Equal(AttachmentStatus.Rejected, row.Status);
            Assert.Equal("Win.Test.EICAR_HDB-1", row.ScanSignature);
            Assert.Null(row.ETag);
        }

        [Fact]
        public async Task Upload_ScannerUnavailable_LeavesAQuarantinedRowAndStoresNothing()
        {
            using AttachmentsDbContext db = NewDb();
            AttachmentsService service = NewService(db);
            _scanner.Unavailable = true;

            await Assert.ThrowsAsync<VirusScanUnavailableException>(() => service.UploadAsync(
                "statement.pdf", "application/pdf", PdfBytes.Length, new MemoryStream(PdfBytes), CancellationToken.None));

            Assert.Empty(_storage.Objects);

            // No verdict, so the row is still quarantined for later cleanup.
            Attachment row = Assert.Single(await db.Attachments.ToListAsync());
            Assert.Equal(AttachmentStatus.Quarantined, row.Status);
        }

        [Fact]
        public async Task ListOwn_ReturnsOnlyTheCallersReleasedAttachments_NewestFirst()
        {
            using AttachmentsDbContext db = NewDb();
            Guid older = await SeedAttachment(db, "user-1", AttachmentStatus.Released, DateTimeOffset.UtcNow.AddHours(-3));
            Guid newer = await SeedAttachment(db, "user-1", AttachmentStatus.Released, DateTimeOffset.UtcNow.AddHours(-1));
            await SeedAttachment(db, "user-1", AttachmentStatus.Rejected, DateTimeOffset.UtcNow.AddHours(-2));
            await SeedAttachment(db, "user-1", AttachmentStatus.Quarantined, DateTimeOffset.UtcNow);
            await SeedAttachment(db, "someone-else", AttachmentStatus.Released, DateTimeOffset.UtcNow);
            AttachmentsService service = NewService(db, subject: "user-1");

            IReadOnlyList<AttachmentResponseModel> list = await service.ListOwnAsync(CancellationToken.None);

            Assert.Equal([newer, older], list.Select(a => a.Id));
        }

        private static AttachmentsDbContext NewDb()
        {
            DbContextOptions<AttachmentsDbContext> options = new DbContextOptionsBuilder<AttachmentsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AttachmentsDbContext(options);
        }

        private AttachmentsService NewService(
            AttachmentsDbContext db,
            string subject = "user-1",
            long maxSizeBytes = 5_242_880)
        {
            return new AttachmentsService(
                NullLogger<AttachmentsService>.Instance,
                db,
                _scanner,
                _storage,
                new StubCurrentUserAccessor(subject),
                Options.Create(new AttachmentsConfig { MaxSizeBytes = maxSizeBytes }));
        }

        private static async Task<Guid> SeedAttachment(
            AttachmentsDbContext db,
            string ownerSubject,
            AttachmentStatus status,
            DateTimeOffset uploadedAt)
        {
            var attachment = new Attachment
            {
                Id = Guid.NewGuid(),
                OwnerSubject = ownerSubject,
                FileName = "seed.pdf",
                ContentType = "application/pdf",
                SizeBytes = 4,
                StorageKey = $"{ownerSubject}/{Guid.NewGuid()}.pdf",
                Status = status,
                UploadedAt = uploadedAt,
            };
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync();
            return attachment.Id;
        }
    }
}
