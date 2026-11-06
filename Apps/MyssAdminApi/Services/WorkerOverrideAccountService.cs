namespace Myss.AdminApi.Services
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Myss.Data;

    /// <summary>
    /// Worker override accounts over the forms schema's profile table (MYSS-321).
    /// </summary>
    public class WorkerOverrideAccountService : IWorkerOverrideAccountService
    {
        private readonly FormsDbContext _dbContext;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerOverrideAccountService"/> class.
        /// </summary>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="timeProvider">Injected time provider.</param>
        public WorkerOverrideAccountService(FormsDbContext dbContext, TimeProvider timeProvider)
        {
            _dbContext = dbContext;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public async Task<Guid> SaveAsync(
            string subject,
            WorkerOverrideClient client,
            string idir,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(client);
            RequireOverrideSubject(subject);

            DateTimeOffset now = _timeProvider.GetUtcNow();
            MyssUserProfile? profile = await _dbContext.MyssUserProfiles
                .Include(p => p.Phones)
                .SingleOrDefaultAsync(p => p.Subject == subject, cancellationToken);
            if (profile is null)
            {
                // Not verified: nobody has proven the address, and no link goes
                // to it. No PIN: nobody signs in. No ICM contact match.
                profile = new MyssUserProfile
                {
                    Id = Guid.NewGuid(),
                    Subject = subject,
                    FirstName = client.FirstName,
                    LastName = client.LastName,
                    DateOfBirth = client.DateOfBirth,
                    Email = client.Email,
                    Sin = client.Sin,
                    Phone = client.Phone,
                    Origin = ProfileOrigins.WorkerOverride,
                    CreatedByIdir = idir,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                _dbContext.MyssUserProfiles.Add(profile);
            }
            else
            {
                profile.FirstName = client.FirstName;
                profile.LastName = client.LastName;
                profile.DateOfBirth = client.DateOfBirth;
                profile.Email = client.Email;
                profile.Sin = client.Sin;
                profile.Phone = client.Phone;
                profile.UpdatedAt = now;
            }

            // The number as the Cell number on Account Info's list, as
            // registration puts it there.
            MyssUserPhone? cell = profile.Phones.FirstOrDefault(p => p.Type == AccountPhoneType.Cell);
            if (cell is null)
            {
                // Added explicitly: a row whose key is already set would
                // otherwise be taken for an existing one.
                _dbContext.Add(new MyssUserPhone
                {
                    Id = Guid.NewGuid(),
                    ProfileId = profile.Id,
                    Type = AccountPhoneType.Cell,
                    Number = client.Phone,
                    Position = profile.Phones.Count,
                });
            }
            else
            {
                cell.Number = client.Phone;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return profile.Id;
        }

        /// <inheritdoc/>
        public async Task<Guid?> FindAsync(string subject, CancellationToken cancellationToken)
        {
            RequireOverrideSubject(subject);
            return await _dbContext.MyssUserProfiles
                .AsNoTracking()
                .Where(p => p.Subject == subject)
                .Select(p => (Guid?)p.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Never a citizen's own account: only a subject no sign-in can have.
        /// </summary>
        private static void RequireOverrideSubject(string subject)
        {
            ArgumentNullException.ThrowIfNull(subject);
            if (!subject.StartsWith(ProfileOrigins.WorkerOverrideSubjectPrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException("Not a worker override subject.", nameof(subject));
            }
        }
    }
}
