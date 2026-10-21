namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
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
    /// Resets a forgotten PIN by email (MYSS-36). The token follows the
    /// registration invite pattern (RULE-IDA-02, BR-D7-09): a GUID, single-use,
    /// nulled when consumed. Only its hash is stored, so the column alone does not
    /// open anyone's account, and it expires after
    /// <see cref="PinResetConfig.TokenLifetimeMinutes"/>.
    /// </summary>
    public class PinResetService : IPinResetService
    {
        /// <summary>
        /// How many times a request is tried when the profile's concurrency
        /// tokens changed between its read and its write. Each try reads afresh.
        /// </summary>
        private const int MaxRequestTries = 3;

        private readonly ILogger<PinResetService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IEmailProvider _emailProvider;
        private readonly TimeProvider _timeProvider;
        private readonly IPinHasher _pinHasher;
        private readonly PinResetConfig _config;
        private readonly IErrorMessageProvider _errorMessageProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PinResetService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">The forms-module data context, which holds the profile.</param>
        /// <param name="currentUserAccessor">The caller accessor.</param>
        /// <param name="emailProvider">Sends the link.</param>
        /// <param name="timeProvider">The clock the link's expiry is measured by.</param>
        /// <param name="pinHasher">The PIN hasher.</param>
        /// <param name="config">The link's lifetime and base URL.</param>
        /// <param name="errorMessageProvider">The error message catalogue.</param>
        public PinResetService(
            ILogger<PinResetService> logger,
            FormsDbContext dbContext,
            ICurrentUserAccessor currentUserAccessor,
            IEmailProvider emailProvider,
            TimeProvider timeProvider,
            IPinHasher pinHasher,
            PinResetConfig config,
            IErrorMessageProvider errorMessageProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _currentUserAccessor = currentUserAccessor;
            _emailProvider = emailProvider;
            _timeProvider = timeProvider;
            _pinHasher = pinHasher;
            _config = config;
            _errorMessageProvider = errorMessageProvider;
        }

        /// <inheritdoc/>
        public async Task<PinResetResultModel> RequestAsync(CancellationToken cancellationToken)
        {
            CurrentUser user = _currentUserAccessor.User;
            if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.Subject))
            {
                throw new InvalidOperationException("An authenticated identity is required to request a PIN reset.");
            }

            // The save below lands only if the profile's concurrency tokens (the
            // reset token and the PIN columns) are as this request read them. One
            // that loses the race starts again from what the winner wrote: after
            // another reset request that is the cooldown, after a PIN change it
            // goes ahead. The race is lost at the save, before any email is sent.
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await TryRequestAsync(user, cancellationToken);
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxRequestTries)
                {
                    _dbContext.ChangeTracker.Clear();
                }
            }
        }

        private async Task<PinResetResultModel> TryRequestAsync(CurrentUser user, CancellationToken cancellationToken)
        {
            MyssUserProfile? profile = await _dbContext.MyssUserProfiles
                .SingleOrDefaultAsync(p => p.Subject == user.Subject, cancellationToken);
            if (profile is null)
            {
                return PinResetResultModel.Refused(PinResetOutcome.ProfileRequired);
            }

            if (!AccountService.UsesPin(user))
            {
                return PinResetResultModel.Refused(PinResetOutcome.PinNotAvailable);
            }

            // One email per cooldown, counted from the stored request time so a
            // restart or a second instance does not reset it. A used link clears
            // that time, so a citizen who has just reset can ask again at once.
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (profile.PinResetRequestedAt is { } lastRequest && now < lastRequest + _config.RequestCooldown)
            {
                return PinResetResultModel.TooSoon(lastRequest + _config.RequestCooldown);
            }

            // Checked here rather than at startup, as the Notify key is, so a
            // checkout without it still boots. A deployment that forgot it gets
            // the same logged 503 as one Notify refuses, and nothing is saved, so
            // no cooldown starts on an email that never went out.
            if (_config.LinkBaseUrl is null)
            {
                _logger.LogError(
                    "No PIN reset email for profile {ProfileId}: Account:PinReset:LinkBaseUrl is not set "
                    + "(deployed: Myss_Account__PinReset__LinkBaseUrl)",
                    profile.Id);
                return PinResetResultModel.Refused(PinResetOutcome.EmailNotSent);
            }

            // Random bytes from the CSPRNG rather than Guid.NewGuid, whose
            // randomness source is a platform detail; the token is still a GUID.
            var token = new Guid(RandomNumberGenerator.GetBytes(16));
            Uri link = _config.BuildLink(token);

            // Saved before sending, so the link works the moment it arrives. A
            // new request replaces the token, which kills any earlier link.
            profile.PinResetTokenHash = HashToken(token);
            profile.PinResetRequestedAt = now;
            profile.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await _emailProvider.SendAsync(ResetEmail(profile.Email, link), cancellationToken);
            }
            catch (EmailNotSentException ex)
            {
                // The token stays: nobody received it, and asking again after the
                // cooldown replaces it.
                _logger.LogError(ex, "PIN reset email for profile {ProfileId} was not sent", profile.Id);
                return PinResetResultModel.Refused(PinResetOutcome.EmailNotSent);
            }

            _logger.LogInformation("Sent a PIN reset link for profile {ProfileId}", profile.Id);
            return PinResetResultModel.Sent(now + _config.TokenLifetime);
        }

        /// <inheritdoc/>
        public async Task<PinResetResultModel> ResetAsync(ResetPinRequestModel request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // The link is checked before the PIN, so a dead link is reported
            // as such rather than after the citizen fixes their typing.
            if (!Guid.TryParse(request.Token, out Guid token))
            {
                return PinResetResultModel.Refused(PinResetOutcome.LinkInvalid);
            }

            string tokenHash = HashToken(token);
            MyssUserProfile? profile = await _dbContext.MyssUserProfiles
                .SingleOrDefaultAsync(p => p.PinResetTokenHash == tokenHash, cancellationToken);
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (profile is null
                || profile.PinResetRequestedAt is not { } requestedAt
                || now >= requestedAt + _config.TokenLifetime)
            {
                return PinResetResultModel.Refused(PinResetOutcome.LinkInvalid);
            }

            var errors = new List<ValidationErrorModel>();
            DomainValidationResult<Pin> newPin = Pin.TryCreate(request.NewPin);
            if (!newPin.IsValid)
            {
                errors.Add(Error("newPin", newPin.Keyword!, newPin.Message!));
            }
            else if (!string.Equals(request.ConfirmPin, newPin.Value!.Digits, StringComparison.Ordinal))
            {
                errors.Add(Error("confirmPin", ValidationKeywords.PinMismatch, "The two PINs do not match."));
            }

            // A mistyped PIN does not use up the link.
            if (errors.Count > 0)
            {
                return PinResetResultModel.Invalid(ErrorMessageResolver.Resolve(
                    errors,
                    await _errorMessageProvider.GetMessagesAsync(cancellationToken)));
            }

            // A reset is the way out of a lockout, so it clears the count too.
            profile.PinHash = _pinHasher.Hash(newPin.Value!);
            profile.PinFailedAttempts = 0;
            profile.PinLockedUntil = null;
            profile.PinResetTokenHash = null;
            profile.PinResetRequestedAt = null;
            profile.UpdatedAt = now;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The token (or the PIN) changed since the read: the same link
                // was used at once from elsewhere, or a newer one was sent.
                return PinResetResultModel.Refused(PinResetOutcome.LinkInvalid);
            }

            _logger.LogInformation("Reset the PIN for profile {ProfileId} from an emailed link", profile.Id);
            return PinResetResultModel.Reset();
        }

        /// <summary>
        /// SHA-256 of the token's canonical text, lower-case hex. Unsalted on
        /// purpose: the token is 128 random bits, so there is nothing to
        /// precompute, and an unsalted hash can be looked up by.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>64 hex characters.</returns>
        private static string HashToken(Guid token) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token.ToString("D"))));

        private EmailMessage ResetEmail(string to, Uri link)
        {
            string lifetime = Describe(_config.TokenLifetime);
            string body =
                "We received a request to reset the PIN for your My Self Serve account.\n\n"
                + $"[Reset your PIN]({link.AbsoluteUri})\n\n"
                + $"The link works once and expires in {lifetime}. "
                + "If it has expired, sign in to My Self Serve and choose Reset PIN on Account Info to get a new one.\n\n"
                + "If you did not ask to reset your PIN, you can ignore this email. Your PIN has not changed.";
            return new EmailMessage(to, "Reset your My Self Serve PIN", body);
        }

        /// <summary>"90 minutes", "1 hour", "24 hours".</summary>
        private static string Describe(TimeSpan lifetime)
        {
            int minutes = (int)lifetime.TotalMinutes;
            if (minutes >= 60 && minutes % 60 == 0)
            {
                int hours = minutes / 60;
                return hours.ToString(CultureInfo.InvariantCulture) + (hours == 1 ? " hour" : " hours");
            }

            return minutes.ToString(CultureInfo.InvariantCulture) + (minutes == 1 ? " minute" : " minutes");
        }

        private static ValidationErrorModel Error(string field, string keyword, string message)
        {
            return new ValidationErrorModel { Field = field, Keyword = keyword, Message = message };
        }
    }
}
