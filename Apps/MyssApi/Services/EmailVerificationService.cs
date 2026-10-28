namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Common.Auth;
    using Myss.Data;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Proves a citizen's email address by an emailed single-use link (MYSS-305),
    /// on the PIN reset's token pattern (<see cref="EmailedLinkToken"/>). One
    /// outstanding link per profile, for <see cref="MyssUserProfile.PendingEmail"/>
    /// when a change is waiting and otherwise for an unverified
    /// <see cref="MyssUserProfile.Email"/>. A verified address is never replaced
    /// until its successor is proven, and is told when it has been.
    /// </summary>
    public class EmailVerificationService : IEmailVerificationService
    {
        /// <summary>
        /// How many times a write is tried when the profile's concurrency tokens
        /// changed between its read and its write. Each try reads afresh.
        /// </summary>
        private const int MaxWriteTries = 3;

        private readonly ILogger<EmailVerificationService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IEmailProvider _emailProvider;
        private readonly TimeProvider _timeProvider;
        private readonly EmailVerificationConfig _config;
        private readonly PinResetConfig _links;
        private readonly IErrorMessageProvider _errorMessageProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailVerificationService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">The forms-module data context, which holds the profile.</param>
        /// <param name="currentUserAccessor">The caller accessor.</param>
        /// <param name="emailProvider">Sends the links and the change notice.</param>
        /// <param name="timeProvider">The clock expiry and cooldown are measured by.</param>
        /// <param name="config">The link's lifetime and the cooldown.</param>
        /// <param name="links">Where emailed links point: the webclient's public URL.</param>
        /// <param name="errorMessageProvider">The error message catalogue.</param>
        public EmailVerificationService(
            ILogger<EmailVerificationService> logger,
            FormsDbContext dbContext,
            ICurrentUserAccessor currentUserAccessor,
            IEmailProvider emailProvider,
            TimeProvider timeProvider,
            EmailVerificationConfig config,
            PinResetConfig links,
            IErrorMessageProvider errorMessageProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _currentUserAccessor = currentUserAccessor;
            _emailProvider = emailProvider;
            _timeProvider = timeProvider;
            _config = config;
            _links = links;
            _errorMessageProvider = errorMessageProvider;
        }

        /// <inheritdoc/>
        public async Task<EmailVerificationResultModel> RequestChangeAsync(
            ChangeEmailRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            DomainValidationResult<EmailAddress> address = EmailAddress.TryCreate(request.Email);
            if (!address.IsValid)
            {
                var error = new ValidationErrorModel { Field = "email", Keyword = address.Keyword!, Message = address.Message! };
                return EmailVerificationResultModel.Invalid(ErrorMessageResolver.Resolve(
                    [error],
                    await _errorMessageProvider.GetMessagesAsync(cancellationToken)));
            }

            string newEmail = address.Value!.Value;
            return await WithRetryAsync(
                profile =>
                {
                    // Back to the address already in use: nothing to prove, so
                    // any pending change, and its link, simply goes.
                    if (profile.EmailVerifiedAt is not null && SameAddress(newEmail, profile.Email))
                    {
                        return new Plan(Step.SaveOnly, ClearPending);
                    }

                    // An unverified address was never relied on, so it is
                    // replaced; a verified one stays in use until its successor
                    // is proven.
                    return new Plan(Step.SendLink, p =>
                    {
                        if (p.EmailVerifiedAt is null)
                        {
                            p.Email = newEmail;
                        }
                        else
                        {
                            p.PendingEmail = newEmail;
                        }
                    });
                },
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task<EmailVerificationResultModel> ResendAsync(CancellationToken cancellationToken)
        {
            return WithRetryAsync(
                profile => VerificationTarget(profile) is null ? new Plan(Step.NothingToVerify) : new Plan(Step.SendLink),
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<EmailVerificationResultModel> CancelChangeAsync(CancellationToken cancellationToken)
        {
            MyssUserProfile? profile = await FindOwnProfileAsync(cancellationToken);
            if (profile is null)
            {
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.ProfileRequired);
            }

            // Only a pending change can be dropped. An unverified address from
            // registration is the only address there is, so it stays.
            if (profile.PendingEmail is not null)
            {
                ClearPending(profile);
                profile.UpdatedAt = _timeProvider.GetUtcNow();
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Cancelled the pending email change for profile {ProfileId}", profile.Id);
            }

            return EmailVerificationResultModel.Done;
        }

        /// <inheritdoc/>
        public async Task<EmailVerificationResultModel> VerifyAsync(
            VerifyEmailRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!EmailedLinkToken.TryHash(request.Token, out string tokenHash))
            {
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.LinkInvalid);
            }

            MyssUserProfile? profile = await _dbContext.MyssUserProfiles
                .SingleOrDefaultAsync(p => p.EmailVerificationTokenHash == tokenHash, cancellationToken);
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (profile is null)
            {
                // Not a profile's link: perhaps registration's (MYSS-295),
                // sent before the profile exists. Same page, same endpoint.
                return await VerifyRegistrationAsync(tokenHash, now, cancellationToken);
            }

            if (profile.EmailVerificationRequestedAt is not { } requestedAt
                || now >= requestedAt + _config.TokenLifetime)
            {
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.LinkInvalid);
            }

            // The address being replaced is told only when it was itself proven
            // and is really going: an unverified one may never have been theirs.
            string? previous = profile.PendingEmail is { } pending
                && profile.EmailVerifiedAt is not null
                && !SameAddress(pending, profile.Email)
                    ? profile.Email
                    : null;

            if (profile.PendingEmail is not null)
            {
                profile.Email = profile.PendingEmail;
                profile.PendingEmail = null;
            }

            profile.EmailVerifiedAt = now;
            profile.EmailVerificationTokenHash = null;
            profile.EmailVerificationRequestedAt = null;
            profile.UpdatedAt = now;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The token changed since the read: the same link was used at
                // once from elsewhere, or a newer one was sent.
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.LinkInvalid);
            }

            _logger.LogInformation(
                "Verified the email address for profile {ProfileId}{Change}",
                profile.Id,
                previous is null ? string.Empty : " (changed)");

            if (previous is not null)
            {
                await SendChangeNoticeAsync(profile, previous, now, cancellationToken);
            }

            return EmailVerificationResultModel.Done;
        }

        /// <inheritdoc/>
        public async Task<EmailVerificationResultModel> RequestRegistrationEmailAsync(
            ChangeEmailRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            DomainValidationResult<EmailAddress> address = EmailAddress.TryCreate(request.Email);
            if (!address.IsValid)
            {
                var error = new ValidationErrorModel { Field = "email", Keyword = address.Keyword!, Message = address.Message! };
                return EmailVerificationResultModel.Invalid(ErrorMessageResolver.Resolve(
                    [error],
                    await _errorMessageProvider.GetMessagesAsync(cancellationToken)));
            }

            string email = address.Value!.Value;
            string subject = OwnSubject();

            // Registration's address proof is only for someone without an
            // account. An account holder's address changes on Account Info,
            // where the old address stays in use until the new one is proven and
            // is told when it goes; letting this path prove an address for them
            // would let a resubmitted registration skip all of that.
            if (await _dbContext.MyssUserProfiles.AnyAsync(p => p.Subject == subject, cancellationToken))
            {
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.ProfileExists);
            }

            RegistrationEmailVerification? row = await _dbContext.RegistrationEmailVerifications
                .SingleOrDefaultAsync(r => r.Subject == subject, cancellationToken);
            DateTimeOffset now = _timeProvider.GetUtcNow();

            // Already proven in this registration (AC6): nothing to send. Only
            // within the registration session; an older proof is proven afresh.
            if (IsFreshProof(row, now) && SameAddress(email, row!.Email))
            {
                return EmailVerificationResultModel.Done;
            }

            // One email per cooldown, as on Account Info, whatever the address:
            // otherwise a signed-in caller could send to any number of them.
            if (row is { TokenHash: not null } && now < row.RequestedAt + _config.RequestCooldown)
            {
                return EmailVerificationResultModel.TooSoon(row.RequestedAt + _config.RequestCooldown);
            }

            if (_links.LinkBaseUrl is null)
            {
                _logger.LogError(
                    "No registration email verification link: Account:PinReset:LinkBaseUrl is not set "
                    + "(deployed: Myss_Account__PinReset__LinkBaseUrl)");
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.EmailNotSent);
            }

            Guid token = EmailedLinkToken.New();
            if (row is null)
            {
                row = new RegistrationEmailVerification { Subject = subject, Email = email };
                _dbContext.RegistrationEmailVerifications.Add(row);
            }

            row.Email = email;
            row.VerifiedAt = null;
            row.TokenHash = EmailedLinkToken.Hash(token);
            row.RequestedAt = now;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Another request for this registration saved first (the row's
                // key, or its token, changed under this one) and is sending now.
                return EmailVerificationResultModel.TooSoon(now + _config.RequestCooldown);
            }

            try
            {
                await _emailProvider.SendAsync(VerificationEmail(email, token, forRegistration: true), cancellationToken);
            }
            catch (EmailNotSentException ex)
            {
                _logger.LogError(ex, "Registration email verification link was not sent");
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.EmailNotSent);
            }

            _logger.LogInformation("Sent a registration email verification link");
            return EmailVerificationResultModel.Done;
        }

        /// <inheritdoc/>
        public async Task<RegistrationEmailModel> GetRegistrationEmailAsync(CancellationToken cancellationToken)
        {
            string subject = OwnSubject();

            // Nothing for an account holder: registration's proof is not theirs to use.
            if (await _dbContext.MyssUserProfiles.AnyAsync(p => p.Subject == subject, cancellationToken))
            {
                return new RegistrationEmailModel { Email = null, Verified = false };
            }

            RegistrationEmailVerification? row = await _dbContext.RegistrationEmailVerifications
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Subject == subject, cancellationToken);
            return new RegistrationEmailModel
            {
                Email = row?.Email,
                Verified = IsFreshProof(row, _timeProvider.GetUtcNow()),
            };
        }

        /// <inheritdoc/>
        public async Task<DateTimeOffset?> RegistrationEmailVerifiedAtAsync(string email, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(email);
            string subject = OwnSubject();

            // An account holder registering again can only keep the verified
            // address the account already has; a different one is changed on
            // Account Info, never through registration.
            MyssUserProfile? profile = await _dbContext.MyssUserProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.Subject == subject, cancellationToken);
            if (profile is not null)
            {
                return profile.EmailVerifiedAt is { } profileVerifiedAt && SameAddress(email, profile.Email)
                    ? profileVerifiedAt
                    : null;
            }

            RegistrationEmailVerification? row = await _dbContext.RegistrationEmailVerifications
                .SingleOrDefaultAsync(r => r.Subject == subject, cancellationToken);
            return IsFreshProof(row, _timeProvider.GetUtcNow()) && SameAddress(email, row!.Email)
                ? row.VerifiedAt
                : null;
        }

        /// <inheritdoc/>
        public async Task ForgetRegistrationEmailAsync(CancellationToken cancellationToken)
        {
            string subject = OwnSubject();
            RegistrationEmailVerification? row = await _dbContext.RegistrationEmailVerifications
                .FindAsync([subject], cancellationToken);
            if (row is not null)
            {
                _dbContext.RegistrationEmailVerifications.Remove(row);
            }
        }

        /// <summary>
        /// Deletes the registration proofs no registration can use any more:
        /// every row whose link, or whose proof, is older than the link's
        /// lifetime (the registration session, AC6). The retention for this
        /// table (MYSS-295, PII inventory): a completed registration deletes its
        /// row at once, an abandoned one is gone within a lifetime and the
        /// purge interval.
        /// </summary>
        /// <param name="dbContext">The forms context.</param>
        /// <param name="now">The current time.</param>
        /// <param name="lifetime">The link's lifetime.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>How many rows were deleted.</returns>
        public static async Task<int> PurgeStaleRegistrationEmailsAsync(
            FormsDbContext dbContext,
            DateTimeOffset now,
            TimeSpan lifetime,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            DateTimeOffset cutoff = now - lifetime;
            List<RegistrationEmailVerification> stale = await dbContext.RegistrationEmailVerifications
                .Where(r => (r.VerifiedAt ?? r.RequestedAt) <= cutoff)
                .ToListAsync(cancellationToken);
            dbContext.RegistrationEmailVerifications.RemoveRange(stale);
            await dbContext.SaveChangesAsync(cancellationToken);
            return stale.Count;
        }

        /// <summary>
        /// A registration proof still within the registration session: verified,
        /// and less than a link's lifetime ago. An older one says nothing about
        /// the citizen registering now.
        /// </summary>
        private bool IsFreshProof(RegistrationEmailVerification? row, DateTimeOffset now) =>
            row is { VerifiedAt: { } verifiedAt } && now < verifiedAt + _config.TokenLifetime;

        /// <inheritdoc/>
        public void ApplyVerifiedRegistrationEmail(MyssUserProfile profile, string email, DateTimeOffset verifiedAt)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(email);

            // Proven before the profile existed, so it is the address in use at
            // once, and any earlier pending change or link goes.
            profile.Email = email.Trim();
            profile.EmailVerifiedAt = verifiedAt;
            profile.PendingEmail = null;
            profile.EmailVerificationTokenHash = null;
            profile.EmailVerificationRequestedAt = null;
        }

        /// <summary>
        /// Emails the link for a token just issued and saved on a profile. A
        /// failure is logged and reported, not thrown.
        /// </summary>
        private async Task<bool> SendIssuedLinkAsync(MyssUserProfile profile, Guid token, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profile);
            try
            {
                await _emailProvider.SendAsync(
                    VerificationEmail(VerificationTarget(profile)!, token, forRegistration: false),
                    cancellationToken);
            }
            catch (EmailNotSentException ex)
            {
                _logger.LogError(ex, "Email verification link for profile {ProfileId} was not sent", profile.Id);
                return false;
            }

            _logger.LogInformation("Sent an email verification link for profile {ProfileId}", profile.Id);
            return true;
        }

        /// <summary>
        /// The address a link proves: the pending change, or else an unverified
        /// address in use. Null when the address in use is verified and nothing
        /// is pending.
        /// </summary>
        private static string? VerificationTarget(MyssUserProfile profile) =>
            profile.PendingEmail ?? (profile.EmailVerifiedAt is null ? profile.Email : null);

        /// <summary>
        /// Addresses compare as <see cref="EmailAddress.ConfirmationMatches"/>
        /// does: trimmed and case-insensitively.
        /// </summary>
        private static bool SameAddress(string a, string b) => EmailAddress.ConfirmationMatches(a, b);

        private static void ClearPending(MyssUserProfile profile)
        {
            profile.PendingEmail = null;
            if (profile.EmailVerifiedAt is not null)
            {
                profile.EmailVerificationTokenHash = null;
                profile.EmailVerificationRequestedAt = null;
            }
        }

        /// <summary>
        /// Gives the profile a new token for its verification target, which kills
        /// any earlier link. Null when no link can be built.
        /// </summary>
        private Guid? Issue(MyssUserProfile profile)
        {
            if (_links.LinkBaseUrl is null)
            {
                LogNoLinkBaseUrl(profile);
                return null;
            }

            Guid token = EmailedLinkToken.New();
            DateTimeOffset now = _timeProvider.GetUtcNow();
            profile.EmailVerificationTokenHash = EmailedLinkToken.Hash(token);
            profile.EmailVerificationRequestedAt = now;
            profile.UpdatedAt = now;
            return token;
        }

        /// <summary>
        /// One signed-in write: find the caller's profile and ask
        /// <paramref name="plan"/> what the request means for it. A link is sent
        /// only after the cooldown check, with the change applied and a token
        /// issued and saved first. Retried from a fresh read when the profile
        /// changed in between, as the PIN reset is; the race is lost at the
        /// save, before any email goes.
        /// </summary>
        private async Task<EmailVerificationResultModel> WithRetryAsync(
            Func<MyssUserProfile, Plan> plan,
            CancellationToken cancellationToken)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await TryWriteAsync(plan, cancellationToken);
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxWriteTries)
                {
                    _dbContext.ChangeTracker.Clear();
                }
            }
        }

        private async Task<EmailVerificationResultModel> TryWriteAsync(
            Func<MyssUserProfile, Plan> plan,
            CancellationToken cancellationToken)
        {
            MyssUserProfile? profile = await FindOwnProfileAsync(cancellationToken);
            if (profile is null)
            {
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.ProfileRequired);
            }

            Plan next = plan(profile);
            DateTimeOffset now = _timeProvider.GetUtcNow();
            switch (next.Step)
            {
                case Step.NothingToVerify:
                    return EmailVerificationResultModel.Refused(EmailVerificationOutcome.NothingToVerify);

                case Step.SaveOnly:
                    next.Apply?.Invoke(profile);
                    profile.UpdatedAt = now;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return EmailVerificationResultModel.Done;
            }

            // One email per cooldown, from the stored send time, so a restart or
            // a second instance does not reset it. Checked before anything
            // changes, so a refused request leaves the earlier link working.
            if (profile.EmailVerificationTokenHash is not null
                && profile.EmailVerificationRequestedAt is { } lastSent
                && now < lastSent + _config.RequestCooldown)
            {
                return EmailVerificationResultModel.TooSoon(lastSent + _config.RequestCooldown);
            }

            // Checked before saving, so a deployment without it changes nothing
            // and starts no cooldown on an email that never went out.
            if (_links.LinkBaseUrl is null)
            {
                LogNoLinkBaseUrl(profile);
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.EmailNotSent);
            }

            next.Apply?.Invoke(profile);
            Guid token = Issue(profile)!.Value;

            // Saved before sending, so the link works the moment it arrives.
            await _dbContext.SaveChangesAsync(cancellationToken);

            return await SendIssuedLinkAsync(profile, token, cancellationToken)
                ? EmailVerificationResultModel.Done
                : EmailVerificationResultModel.Refused(EmailVerificationOutcome.EmailNotSent);
        }

        private async Task<MyssUserProfile?> FindOwnProfileAsync(CancellationToken cancellationToken)
        {
            string subject = OwnSubject();
            return await _dbContext.MyssUserProfiles
                .SingleOrDefaultAsync(p => p.Subject == subject, cancellationToken);
        }

        private string OwnSubject()
        {
            CurrentUser user = _currentUserAccessor.User;
            if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.Subject))
            {
                throw new InvalidOperationException("An authenticated identity is required to change an email address.");
            }

            return user.Subject;
        }

        /// <summary>
        /// Follows a registration link (MYSS-295): the address is proven for the
        /// registration still to be completed. Nothing is emailed: there is no
        /// earlier address to tell.
        /// </summary>
        private async Task<EmailVerificationResultModel> VerifyRegistrationAsync(
            string tokenHash,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            RegistrationEmailVerification? row = await _dbContext.RegistrationEmailVerifications
                .SingleOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);
            if (row is null || now >= row.RequestedAt + _config.TokenLifetime)
            {
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.LinkInvalid);
            }

            row.VerifiedAt = now;
            row.TokenHash = null;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return EmailVerificationResultModel.Refused(EmailVerificationOutcome.LinkInvalid);
            }

            _logger.LogInformation("Verified a registration email address");
            return EmailVerificationResultModel.Done;
        }

        private void LogNoLinkBaseUrl(MyssUserProfile profile)
        {
            _logger.LogError(
                "No email verification link for profile {ProfileId}: Account:PinReset:LinkBaseUrl is not set "
                + "(deployed: Myss_Account__PinReset__LinkBaseUrl)",
                profile.Id);
        }

        private EmailMessage VerificationEmail(string to, Guid token, bool forRegistration)
        {
            Uri link = _links.BuildLink(
                forRegistration ? EmailVerificationConfig.RegistrationLinkPath : EmailVerificationConfig.LinkPath,
                token);
            string lifetime = EmailedLinkToken.Describe(_config.TokenLifetime);
            string body =
                "Confirm that this is your email address for your My Self Serve account.\n\n"
                + $"[Confirm your email address]({link.AbsoluteUri})\n\n"
                + $"The link works once and expires in {lifetime}. "
                + "If it has expired, go back to My Self Serve and choose Resend link.\n\n"
                + "If you did not ask for this, you can ignore this email. Nothing on your account has changed.";
            return new EmailMessage(to, "Confirm your email address for My Self Serve", body);
        }

        /// <summary>
        /// Tells the address just replaced that it was, best-effort: the change
        /// stands either way. It does not name the new address, which this
        /// mailbox may not be entitled to see.
        /// </summary>
        private async Task SendChangeNoticeAsync(
            MyssUserProfile profile,
            string previous,
            DateTimeOffset changedAt,
            CancellationToken cancellationToken)
        {
            string when = changedAt.ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-CA"));
            string body =
                $"The email address for your My Self Serve account was changed on {when}. "
                + "Messages about your account now go to the new address.\n\n"
                + "If you made this change, you do not need to do anything.\n\n"
                + "If you did not make this change, sign in to My Self Serve right away and check your "
                + "contact information, or contact the ministry.";
            try
            {
                await _emailProvider.SendAsync(
                    new EmailMessage(previous, "Your My Self Serve email address was changed", body),
                    cancellationToken);
                _logger.LogInformation("Sent the email change notice for profile {ProfileId}", profile.Id);
            }
            catch (EmailNotSentException ex)
            {
                _logger.LogError(ex, "Email change notice for profile {ProfileId} was not sent", profile.Id);
            }
        }

        /// <summary>What a signed-in request comes to for this profile.</summary>
        /// <param name="Step">How the write finishes.</param>
        /// <param name="Apply">The change to make to the profile, if any.</param>
        private readonly record struct Plan(Step Step, Action<MyssUserProfile>? Apply = null);

        private enum Step
        {
            /// <summary>Apply, issue a token, save, and email the link.</summary>
            SendLink,

            /// <summary>Apply and save; no link is needed.</summary>
            SaveOnly,

            /// <summary>Nothing is waiting to be verified; change nothing.</summary>
            NothingToVerify,
        }
    }
}
