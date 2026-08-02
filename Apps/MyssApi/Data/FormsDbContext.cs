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
        }
    }
}
