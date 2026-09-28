namespace Myss.Api.Tests.TestDoubles
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Intake;

    /// <summary>
    /// The intake context over the InMemory provider, with the same JSON text
    /// conversion as <see cref="InMemoryFormsDbContext"/>. The row-version
    /// concurrency token works unchanged under this provider.
    /// </summary>
    public class InMemoryIntakeDbContext : IntakeDbContext
    {
        /// <summary>Initializes a new instance of the <see cref="InMemoryIntakeDbContext"/> class.</summary>
        /// <param name="options">The context options.</param>
        public InMemoryIntakeDbContext(DbContextOptions<IntakeDbContext> options)
            : base(options)
        {
        }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationAnswers>()
                .Property(a => a.Answers)
                .HasConversion(
                    doc => doc.RootElement.GetRawText(),
                    text => JsonDocument.Parse(text, default(JsonDocumentOptions)));
        }
    }
}
