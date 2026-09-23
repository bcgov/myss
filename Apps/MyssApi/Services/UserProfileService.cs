namespace Myss.Api.Services
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Data;

    /// <summary>
    /// Queries registration profiles owned by the forms module.
    /// </summary>
    public class UserProfileService : IUserProfileService
    {
        private readonly FormsDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProfileService"/> class.
        /// </summary>
        /// <param name="dbContext">The forms-module data context.</param>
        public UserProfileService(FormsDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <inheritdoc/>
        public Task<bool> HasProfileAsync(string subject, CancellationToken cancellationToken)
        {
            return this.dbContext.MyssUserProfiles
                .AsNoTracking()
                .AnyAsync(profile => profile.Subject == subject, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<string?> GetFirstNameAsync(string subject, CancellationToken cancellationToken)
        {
            return this.dbContext.MyssUserProfiles
                .AsNoTracking()
                .Where(profile => profile.Subject == subject)
                .Select(profile => profile.FirstName)
                .SingleOrDefaultAsync(cancellationToken);
        }
    }
}
