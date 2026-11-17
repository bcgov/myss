namespace Myss.Api.Tests.TestDoubles
{
    using Microsoft.EntityFrameworkCore;
    using Myss.Data;

    /// <summary>
    /// Lets another request write between a request's read of the profile and
    /// its write: the save is the step in between. For the races the PIN reset
    /// and email verification guard against.
    /// </summary>
    /// <param name="options">The context options.</param>
    public sealed class InterruptibleFormsDbContext(DbContextOptions<FormsDbContext> options)
        : InMemoryFormsDbContext(options)
    {
        /// <summary>Gets or sets what runs once, just before the next save.</summary>
        public Func<Task>? BeforeNextSave { get; set; }

        /// <inheritdoc/>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Func<Task>? interruption = BeforeNextSave;
            BeforeNextSave = null;
            if (interruption is not null)
            {
                await interruption();
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
