namespace Myss.Api.Platform
{
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// EF Core context for the platform package. Owns the "platform" schema
    /// only (handbook Part 7.1): today the shared event log, later the audit
    /// table. Domains never use this context directly; they go through
    /// <see cref="IEventStore"/>.
    /// </summary>
    public class PlatformDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlatformDbContext"/> class.
        /// </summary>
        /// <param name="options">Injected context options.</param>
        public PlatformDbContext(DbContextOptions<PlatformDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets the event log.
        /// </summary>
        public DbSet<Event> Events => Set<Event>();

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("platform");

            var evt = modelBuilder.Entity<Event>();
            evt.ToTable("event");
            evt.HasKey(e => new { e.StreamId, e.Version });
            evt.Property(e => e.StreamId).HasColumnName("stream_id");
            evt.Property(e => e.Version).HasColumnName("version");
            evt.Property(e => e.Type).HasColumnName("type").HasMaxLength(64).IsRequired();
            evt.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            evt.Property(e => e.Actor).HasColumnName("actor").HasMaxLength(255).IsRequired();
            evt.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            evt.Property(e => e.RequestId).HasColumnName("request_id").HasMaxLength(128);
        }
    }
}
