namespace Myss.Api.Tests.TestDoubles
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Data;

    /// <summary>
    /// <see cref="FormsDbContext"/> with the one adjustment the InMemory
    /// provider needs.
    /// </summary>
    /// <remarks>
    /// Npgsql maps <see cref="JsonDocument"/> to <c>jsonb</c> natively; the
    /// InMemory provider has no such mapping and throws at first use. Storing
    /// the raw JSON as a string keeps the tests honest about the shape of the
    /// data without pretending to test the Postgres mapping — which only a real
    /// database can prove.
    /// </remarks>
    public class InMemoryFormsDbContext : FormsDbContext
    {
        /// <summary>Initializes a new instance of the <see cref="InMemoryFormsDbContext"/> class.</summary>
        /// <param name="options">The context options.</param>
        public InMemoryFormsDbContext(DbContextOptions<FormsDbContext> options)
            : base(options)
        {
        }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<FormSubmission>()
                .Property(s => s.Answers)
                .HasConversion(
                    doc => doc.RootElement.GetRawText(),
                    text => JsonDocument.Parse(text, default(JsonDocumentOptions)));
        }
    }
}
