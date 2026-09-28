namespace Myss.Api.Intake
{
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// EF Core context for the Application Intake module. Owns the "intake"
    /// schema only, per the schema-per-module rule (handbook Part 7.1), with
    /// its own migrations history table.
    /// </summary>
    public class IntakeDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntakeDbContext"/> class.
        /// </summary>
        /// <param name="options">Injected context options.</param>
        public IntakeDbContext(DbContextOptions<IntakeDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets the applications.
        /// </summary>
        public DbSet<ApplicationAnswers> Applications => Set<ApplicationAnswers>();

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("intake");

            var application = modelBuilder.Entity<ApplicationAnswers>();
            application.ToTable("application_answers");
            application.HasKey(a => a.Id);
            application.Property(a => a.Id).HasColumnName("application_id");
            application.Property(a => a.OwnerSubject).HasColumnName("owner_subject").HasMaxLength(255).IsRequired();
            application.Property(a => a.FormSpecId).HasColumnName("form_spec_id").IsRequired();
            application.Property(a => a.FormSpecVersion).HasColumnName("form_spec_version");
            application.Property(a => a.Answers).HasColumnName("answers").HasColumnType("jsonb").IsRequired();
            application.Property(a => a.Version).HasColumnName("version").IsConcurrencyToken();
            application.Property(a => a.CreatedAt).HasColumnName("created_at");
            application.Property(a => a.UpdatedAt).HasColumnName("updated_at");
            application.HasIndex(a => a.OwnerSubject);
        }
    }
}
