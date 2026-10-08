namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Configuration.Models;
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
        private readonly IPinHasher _pinHasher;
        private readonly PinLockoutConfig _pinLockout;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">The forms-module data context.</param>
        /// <param name="caseAccountProvider">The case details source.</param>
        /// <param name="currentUserAccessor">The caller accessor.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <param name="pinHasher">The PIN hasher.</param>
        /// <param name="pinLockout">How many wrong PINs lock Change PIN, and for how long.</param>
        public AccountService(
            ILogger<AccountService> logger,
            FormsDbContext dbContext,
            ICaseAccountProvider caseAccountProvider,
            ICurrentUserAccessor currentUserAccessor,
            TimeProvider timeProvider,
            IPinHasher pinHasher,
            PinLockoutConfig pinLockout)
        {
            _logger = logger;
            _dbContext = dbContext;
            _caseAccountProvider = caseAccountProvider;
            _currentUserAccessor = currentUserAccessor;
            _timeProvider = timeProvider;
            _pinHasher = pinHasher;
            _pinLockout = pinLockout;
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
                // A JSON body can hold a null entry ({"phones":[null]}) despite the
                // non-nullable annotation; model validation does not look inside
                // the list. Read through it so a null fails both checks below as
                // an empty entry would, a 422 rather than a 500.
                PhoneInputModel? input = request.Phones[i];
                string? rawNumber = input?.Number;
                string? rawType = input?.Type;

                DomainValidationResult<PhoneNumber> number = PhoneNumber.TryCreate(rawNumber);
                if (!number.IsValid)
                {
                    errors.Add(Error($"phones[{i}].number", number.Keyword!, number.Message!));
                }

                // Parsed by name only: Enum.TryParse would also take "0" or "7".
                AccountPhoneType? type = Enum.GetValues<AccountPhoneType>()
                    .Cast<AccountPhoneType?>()
                    .FirstOrDefault(t => string.Equals(t.ToString(), rawType, StringComparison.OrdinalIgnoreCase));
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

        /// <inheritdoc/>
        public async Task<AccountResultModel> SavePinAsync(SavePinRequestModel request, CancellationToken cancellationToken)
        {
            MyssUserProfile? profile = await FindOwnProfileAsync(track: true, cancellationToken);
            if (profile is null)
            {
                return AccountResultModel.ProfileRequired();
            }

            if (!UsesPin(_currentUserAccessor.User))
            {
                return AccountResultModel.PinNotAvailable();
            }

            // Checked before anything else, the current PIN included, so a
            // locked account cannot be used to keep guessing.
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (profile.PinLockedUntil is { } lockedUntil && lockedUntil > now)
            {
                return AccountResultModel.PinLocked(lockedUntil);
            }

            bool isChange = profile.PinHash is not null;
            var errors = new List<ValidationErrorModel>();

            DomainValidationResult<Pin> current = Pin.TryCreate(request.CurrentPin);
            if (isChange && !current.IsValid)
            {
                errors.Add(Error("currentPin", current.Keyword!, current.Message!));
            }

            DomainValidationResult<Pin> newPin = Pin.TryCreate(request.NewPin);
            if (!newPin.IsValid)
            {
                errors.Add(Error("newPin", newPin.Keyword!, newPin.Message!));
            }
            else if (!string.Equals(request.ConfirmPin, newPin.Value!.Digits, StringComparison.Ordinal))
            {
                errors.Add(Error("confirmPin", ValidationKeywords.PinMismatch, "The two PINs do not match."));
            }

            // A malformed request is not a guess, so it does not count as an attempt.
            if (errors.Count > 0)
            {
                return AccountResultModel.Invalid(errors);
            }

            if (isChange && !_pinHasher.Verify(profile.PinHash!, current.Value!, out _))
            {
                return await RecordWrongPinAsync(profile, now, cancellationToken);
            }

            profile.PinHash = _pinHasher.Hash(newPin.Value!);
            profile.PinFailedAttempts = 0;
            profile.PinLockedUntil = null;
            profile.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{PinAction} the PIN for profile {ProfileId}", isChange ? "Changed" : "Created", profile.Id);
            return AccountResultModel.Ok(await ToModelAsync(profile, cancellationToken));
        }

        /// <summary>Only a Basic BCeID sign-in uses a PIN (MYSS-258).</summary>
        private static bool UsesPin(CurrentUser user) => !string.IsNullOrWhiteSpace(user.BceidGuid);

        private async Task<AccountResultModel> RecordWrongPinAsync(
            MyssUserProfile profile,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            profile.PinFailedAttempts++;
            if (profile.PinFailedAttempts >= _pinLockout.MaxAttempts)
            {
                // The count starts again once the lockout ends.
                DateTimeOffset lockedUntil = now + _pinLockout.Lockout;
                profile.PinFailedAttempts = 0;
                profile.PinLockedUntil = lockedUntil;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Locked the PIN for profile {ProfileId} until {LockedUntil} after {MaxAttempts} wrong attempts",
                    profile.Id,
                    lockedUntil,
                    _pinLockout.MaxAttempts);
                return AccountResultModel.PinLocked(lockedUntil);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Wrong current PIN for profile {ProfileId} ({FailedAttempts} of {MaxAttempts})",
                profile.Id,
                profile.PinFailedAttempts,
                _pinLockout.MaxAttempts);
            return AccountResultModel.Invalid([Error(
                "currentPin",
                ValidationKeywords.PinIncorrect,
                "The current PIN is not correct.")]);
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
                PinStatus = PinStatus(profile),
            };
        }

        private AccountPinStatus PinStatus(MyssUserProfile profile)
        {
            if (!UsesPin(_currentUserAccessor.User))
            {
                return AccountPinStatus.NotApplicable;
            }

            return profile.PinHash is null ? AccountPinStatus.NotSet : AccountPinStatus.Set;
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
