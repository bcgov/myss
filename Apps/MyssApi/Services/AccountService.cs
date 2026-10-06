namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Account Info over the registration profile, which the forms module owns.
    /// Contact details are stored with the profile; case details come from
    /// <see cref="ICaseAccountProvider"/>.
    /// </summary>
    public class AccountService : IAccountService
    {
        /// <summary>
        /// The most phone numbers an account holds: one of each <see cref="AccountPhoneType"/>.
        /// A constant rather than the enum's count so the bound is visible where it is checked.
        /// </summary>
        public const int MaxPhones = 4;

        private readonly ILogger<AccountService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly ICaseAccountProvider _caseAccountProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">The forms-module data context.</param>
        /// <param name="caseAccountProvider">The case details source.</param>
        /// <param name="currentUserAccessor">The caller accessor.</param>
        /// <param name="timeProvider">The clock.</param>
        public AccountService(
            ILogger<AccountService> logger,
            FormsDbContext dbContext,
            ICaseAccountProvider caseAccountProvider,
            ICurrentUserAccessor currentUserAccessor,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _caseAccountProvider = caseAccountProvider;
            _currentUserAccessor = currentUserAccessor;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public async Task<AccountResultModel> GetAsync(CancellationToken cancellationToken)
        {
            MyssUserProfile? profile = await FindOwnProfileAsync(track: false, cancellationToken);
            if (profile is null)
            {
                return AccountResultModel.ProfileRequired();
            }

            return AccountResultModel.Ok(await ToModelAsync(profile, cancellationToken));
        }

        /// <inheritdoc/>
        public async Task<AccountResultModel> UpdatePhonesAsync(
            UpdatePhonesRequestModel request,
            CancellationToken cancellationToken)
        {
            MyssUserProfile? profile = await FindOwnProfileAsync(track: true, cancellationToken);
            if (profile is null)
            {
                return AccountResultModel.ProfileRequired();
            }

            // The list length comes from the request body, so bound it before
            // looping over it (Sonar S6680): one number per type means a longer
            // list is never valid, and refusing it here keeps an oversized
            // body from setting how much work the loop does.
            if (request.Phones.Count > MaxPhones)
            {
                return AccountResultModel.Invalid([Error(
                    "phones",
                    ValidationKeywords.PhoneTooMany,
                    $"You can have up to {MaxPhones} phone numbers, one of each type.")]);
            }

            // The check above already guarantees this equals the list length.
            // The loop bound still goes through Math.Min so it is bounded by
            // construction, which is what the taint analysis can see.
            int count = Math.Min(request.Phones.Count, MaxPhones);

            var errors = new List<ValidationErrorModel>();
            var phones = new List<MyssUserPhone>();
            var typesSeen = new HashSet<AccountPhoneType>();
            for (int i = 0; i < count; i++)
            {
                PhoneInputModel input = request.Phones[i];

                DomainValidationResult<PhoneNumber> number = PhoneNumber.TryCreate(input.Number);
                if (!number.IsValid)
                {
                    errors.Add(Error($"phones[{i}].number", number.Keyword!, number.Message!));
                }

                // Parsed by name only: Enum.TryParse would also take "0" or "7".
                AccountPhoneType? type = Enum.GetValues<AccountPhoneType>()
                    .Cast<AccountPhoneType?>()
                    .FirstOrDefault(t => string.Equals(t.ToString(), input.Type, StringComparison.OrdinalIgnoreCase));
                if (type is null)
                {
                    errors.Add(Error(
                        $"phones[{i}].type",
                        ValidationKeywords.PhoneTypeUnknown,
                        "Choose a phone type: home, cell, work or message."));
                }
                else if (!typesSeen.Add(type.Value))
                {
                    errors.Add(Error(
                        $"phones[{i}].type",
                        ValidationKeywords.PhoneTypeDuplicate,
                        "You can have one phone number of each type. Choose a different type."));
                }

                if (number.IsValid && type is not null)
                {
                    phones.Add(new MyssUserPhone
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profile.Id,
                        Type = type.Value,
                        Number = number.Value!.Digits,
                        Position = i,
                    });
                }
            }

            if (errors.Count > 0)
            {
                return AccountResultModel.Invalid(errors);
            }

            // The list replaces the stored one. Added explicitly: a row whose
            // key is already set would otherwise be taken for an existing one.
            // EF deletes before it inserts, so a type kept across the edit does
            // not trip the one-per-type index.
            _dbContext.RemoveRange(profile.Phones);
            _dbContext.AddRange(phones);
            profile.UpdatedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Saved {PhoneCount} phone numbers for profile {ProfileId}", phones.Count, profile.Id);
            return AccountResultModel.Ok(await ToModelAsync(profile, cancellationToken));
        }

        /// <inheritdoc/>
        public async Task<AccountResultModel> UpdateNotificationPreferencesAsync(
            UpdateNotificationPreferencesRequestModel request,
            CancellationToken cancellationToken)
        {
            MyssUserProfile? profile = await FindOwnProfileAsync(track: true, cancellationToken);
            if (profile is null)
            {
                return AccountResultModel.ProfileRequired();
            }

            profile.MonthlyReportReminder = request.MonthlyReportReminder;
            profile.UpdatedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(cancellationToken);

            return AccountResultModel.Ok(await ToModelAsync(profile, cancellationToken));
        }

        private static ValidationErrorModel Error(string field, string keyword, string message)
        {
            return new ValidationErrorModel { Field = field, Keyword = keyword, Message = message };
        }

        private async Task<AccountModel> ToModelAsync(MyssUserProfile profile, CancellationToken cancellationToken)
        {
            CaseAccountDetails caseDetails = await _caseAccountProvider.GetAsync(profile.Subject, cancellationToken);
            return new AccountModel
            {
                CaseNumber = caseDetails.CaseNumber,
                ClientName = $"{profile.FirstName} {profile.LastName}",
                FamilyMembers = caseDetails.FamilyMembers,
                Email = profile.Email,
                Phones = profile.Phones
                    .OrderBy(p => p.Position)
                    .Select(p => new AccountPhoneModel { Number = p.Number, Type = p.Type })
                    .ToList(),
                MailingAddressLines = caseDetails.MailingAddressLines,
                MonthlyReportReminder = profile.MonthlyReportReminder,
            };
        }

        private Task<MyssUserProfile?> FindOwnProfileAsync(bool track, CancellationToken cancellationToken)
        {
            CurrentUser user = _currentUserAccessor.User;
            if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.Subject))
            {
                throw new InvalidOperationException("An authenticated identity is required to read an account.");
            }

            IQueryable<MyssUserProfile> query = _dbContext.MyssUserProfiles.Include(p => p.Phones);
            if (!track)
            {
                query = query.AsNoTracking();
            }

            return query.SingleOrDefaultAsync(p => p.Subject == user.Subject, cancellationToken);
        }
    }
}
