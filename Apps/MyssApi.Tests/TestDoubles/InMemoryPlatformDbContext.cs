namespace Myss.Api.Tests.TestDoubles
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Platform;

    /// <summary>
    /// The platform context over the InMemory provider, which cannot map
    /// <see cref="JsonDocument"/> the way Npgsql does; the payload is stored as
    /// its JSON text instead. The composite key and the rest of the model are
    /// unchanged, so a duplicate (stream, version) still fails on save.
    /// </summary>
    public class InMemoryPlatformDbContext : PlatformDbContext
    {
        /// <summary>Initializes a new instance of the <see cref="InMemoryPlatformDbContext"/> class.</summary>
        /// <param name="options">The context options.</param>
        public InMemoryPlatformDbContext(DbContextOptions<PlatformDbContext> options)
            : base(options)
        {
        }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Event>()
                .Property(e => e.Payload)
                .HasConversion(
                    doc => doc.RootElement.GetRawText(),
                    text => JsonDocument.Parse(text, default(JsonDocumentOptions)));
        }
    }
}
