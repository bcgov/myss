namespace Myss.Api.Data
{
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// EF Core context for the forms module. Owns the "forms" schema only,
    /// per the schema-per-module rule.
    /// </summary>
    public class FormsDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormsDbContext"/> class.
        /// </summary>
        /// <param name="options">Injected context options.</param>
        public FormsDbContext(DbContextOptions<FormsDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets the form submissions set.
        /// </summary>
        public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

        /// <summary>
        /// Gets the bus pass dispatch event log.
        /// </summary>
        public DbSet<BusPassDispatchEvent> BusPassDispatchEvents => Set<BusPassDispatchEvent>();

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("forms");

            var submission = modelBuilder.Entity<FormSubmission>();
            submission.ToTable("form_submissions");
            submission.HasKey(s => s.Id);
            submission.Property(s => s.Id).HasColumnName("id");
            submission.Property(s => s.FormSpecId).HasColumnName("form_spec_id").IsRequired();
            submission.Property(s => s.FormSpecVersion).HasColumnName("form_spec_version");
            submission.Property(s => s.Answers).HasColumnName("answers").HasColumnType("jsonb");
            submission.Property(s => s.SubmittedAt).HasColumnName("submitted_at");
            submission.HasIndex(s => new { s.FormSpecId, s.FormSpecVersion });

            var dispatchEvent = modelBuilder.Entity<BusPassDispatchEvent>();
            dispatchEvent.ToTable("bus_pass_dispatch_events");
            dispatchEvent.HasKey(e => e.Id);
            dispatchEvent.Property(e => e.Id).HasColumnName("id");
            dispatchEvent.Property(e => e.SubmissionId).HasColumnName("submission_id");
            dispatchEvent.Property(e => e.AttemptId).HasColumnName("attempt_id");
            dispatchEvent.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(16);
            dispatchEvent.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            dispatchEvent.Property(e => e.RequestId).HasColumnName("request_id").HasMaxLength(128);
            dispatchEvent.Property(e => e.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(64);
            dispatchEvent.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
            dispatchEvent.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(1024);
            dispatchEvent.HasOne<FormSubmission>()
                .WithMany()
                .HasForeignKey(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Two indexes on the same column need distinct model names, or EF
            // treats the second HasIndex as a reconfiguration of the first.
            dispatchEvent.HasIndex(e => e.SubmissionId, "ix_bus_pass_dispatch_events_submission_id");
            dispatchEvent.HasIndex(e => e.AttemptId, "ix_bus_pass_dispatch_events_attempt_id");

            // At most one accepted hand-off per submission: ICM has no
            // idempotency key, so this is the last line against sending a
            // request twice.
            dispatchEvent.HasIndex(e => e.SubmissionId, "ux_bus_pass_dispatch_events_accepted")
                .IsUnique()
                .HasFilter("type = 'Accepted'");
        }
    }
}
