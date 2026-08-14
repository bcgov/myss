namespace Myss.Api.Data
{
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// EF Core context for the attachments module. Owns the "attachments"
    /// schema only, per the schema-per-module rule. It also gets its own
    /// migrations history table so its migrations don't mix with the forms
    /// ones.
    /// </summary>
    public class AttachmentsDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AttachmentsDbContext"/> class.
        /// </summary>
        /// <param name="options">Injected context options.</param>
        public AttachmentsDbContext(DbContextOptions<AttachmentsDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets the attachments set.
        /// </summary>
        public DbSet<Attachment> Attachments => Set<Attachment>();

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("attachments");

            var attachment = modelBuilder.Entity<Attachment>();
            attachment.ToTable("attachments");
            attachment.HasKey(a => a.Id);
            attachment.Property(a => a.Id).HasColumnName("id");
            attachment.Property(a => a.OwnerSubject).HasColumnName("owner_subject").HasMaxLength(255).IsRequired();
            attachment.Property(a => a.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            attachment.Property(a => a.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
            attachment.Property(a => a.SizeBytes).HasColumnName("size_bytes");
            attachment.Property(a => a.StorageKey).HasColumnName("storage_key").HasMaxLength(255).IsRequired();
            attachment.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
            attachment.Property(a => a.ETag).HasColumnName("etag").HasMaxLength(255);
            attachment.Property(a => a.ScanSignature).HasColumnName("scan_signature").HasMaxLength(255);
            attachment.Property(a => a.SubmissionId).HasColumnName("submission_id");
            attachment.Property(a => a.UploadedAt).HasColumnName("uploaded_at");
            attachment.HasIndex(a => a.OwnerSubject);
        }
    }
}
