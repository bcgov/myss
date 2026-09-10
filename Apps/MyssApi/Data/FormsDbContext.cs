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
        /// Gets the bus pass dispatch attempts set.
        /// </summary>
        public DbSet<BusPassDispatch> BusPassDispatches => Set<BusPassDispatch>();

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

            var dispatch = modelBuilder.Entity<BusPassDispatch>();
            dispatch.ToTable("bus_pass_dispatches");
            dispatch.HasKey(d => d.Id);
            dispatch.Property(d => d.Id).HasColumnName("id");
            dispatch.Property(d => d.SubmissionId).HasColumnName("submission_id");
            dispatch.Property(d => d.AttemptedAt).HasColumnName("attempted_at");
            dispatch.Property(d => d.CompletedAt).HasColumnName("completed_at");
            dispatch.Property(d => d.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(16);
            dispatch.Property(d => d.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(64);
            dispatch.Property(d => d.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
            dispatch.Property(d => d.ErrorMessage).HasColumnName("error_message").HasMaxLength(1024);
            dispatch.HasOne<FormSubmission>()
                .WithMany()
                .HasForeignKey(d => d.SubmissionId)
                .OnDelete(DeleteBehavior.Restrict);
            // Two indexes on the same column need distinct model names, or EF
            // treats the second HasIndex as a reconfiguration of the first.
            dispatch.HasIndex(d => d.SubmissionId, "ix_bus_pass_dispatches_submission_id");

            // At most one accepted hand-off per submission: ICM has no
            // idempotency key, so this is the last line against sending a
            // request twice.
            dispatch.HasIndex(d => d.SubmissionId, "ux_bus_pass_dispatches_accepted")
                .IsUnique()
                .HasFilter("outcome = 'Accepted'");
        }
    }
}
