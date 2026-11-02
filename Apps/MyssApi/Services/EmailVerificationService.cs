namespace Myss.Api.Services
{
    using System;
    using System.Globalization;
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
            if (profile is null
                || profile.EmailVerificationRequestedAt is not { } requestedAt
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
        public Guid? ApplyRegistrationEmail(MyssUserProfile profile, string email)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(email);

            // The form validator accepts surrounding whitespace and checks the
            // trimmed address, so that is the one stored and emailed.
            email = email.Trim();
            bool isNew = string.IsNullOrEmpty(profile.Email);
            if (!isNew && SameAddress(email, profile.Email))
            {
                // Registering again with the address on file (or a new profile
                // already given it): a verified one stays verified, an unverified
                // one is stored as typed now and sent a fresh link.
                if (profile.EmailVerifiedAt is not null)
                {
                    return null;
                }

                profile.Email = email;
                return Issue(profile);
            }

            if (isNew || profile.EmailVerifiedAt is null)
            {
                profile.Email = email;
                profile.PendingEmail = null;
            }
            else
            {
                profile.PendingEmail = email;
            }

            return Issue(profile);
        }

        /// <inheritdoc/>
        public async Task<bool> SendIssuedLinkAsync(MyssUserProfile profile, Guid token, CancellationToken cancellationToken) =>
            await TrySendLinkAsync(profile, token, cancellationToken) == SendResult.Sent;

        /// <summary>
        /// Sends the link for an issued token, and says how it went. Logged,
        /// not thrown.
        /// </summary>
        private async Task<SendResult> TrySendLinkAsync(
            MyssUserProfile profile,
            Guid token,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profile);
            try
            {
                await _emailProvider.SendAsync(VerificationEmail(VerificationTarget(profile)!, token), cancellationToken);
            }
            catch (EmailNotSentException ex)
            {
                _logger.LogError(ex, "Email verification link for profile {ProfileId} was not sent", profile.Id);
                if (ex.RecipientRejected)
                {
                    return SendResult.AddressRejected;
                }

                return ex.MayHaveBeenSent ? SendResult.MayHaveBeenSent : SendResult.NotSent;
            }

            _logger.LogInformation("Sent an email verification link for profile {ProfileId}", profile.Id);
            return SendResult.Sent;
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

            // What the request changes, kept so a link that cannot be sent can
            // be taken back (MYSS-326).
            var before = (profile.Email, profile.PendingEmail, profile.EmailVerificationTokenHash, profile.EmailVerificationRequestedAt, profile.UpdatedAt);

            next.Apply?.Invoke(profile);
            Guid token = Issue(profile)!.Value;

            // Saved before sending, so the link works the moment it arrives.
            await _dbContext.SaveChangesAsync(cancellationToken);

            SendResult sent = await TrySendLinkAsync(profile, token, cancellationToken);
            if (sent == SendResult.Sent)
            {
                return EmailVerificationResultModel.Done;
            }

            // Only when certainly nothing went out does nothing change
            // (MYSS-326): the address in use, any earlier pending change and its
            // link are as they were, and no cooldown starts on an email never
            // sent. When it may have gone out (a timeout, say), the link stays:
            // taking it back could kill one already in the inbox.
            if (sent != SendResult.MayHaveBeenSent)
            {
                (profile.Email, profile.PendingEmail, profile.EmailVerificationTokenHash, profile.EmailVerificationRequestedAt, profile.UpdatedAt) = before;
                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another request wrote since; what it wrote stands.
                }
            }

            if (sent == SendResult.AddressRejected)
            {
                // The citizen's to fix: a field error on the address, in the
                // 422 shape every account form reads.
                var error = new ValidationErrorModel
                {
                    Field = "email",
                    Keyword = ValidationKeywords.EmailAddressRejected,
                    Message = "We could not send an email to that address. Check it and try again.",
                };
                return EmailVerificationResultModel.Invalid(ErrorMessageResolver.Resolve(
                    [error],
                    await _errorMessageProvider.GetMessagesAsync(cancellationToken)));
            }

            return EmailVerificationResultModel.Refused(EmailVerificationOutcome.EmailNotSent);
        }

        private async Task<MyssUserProfile?> FindOwnProfileAsync(CancellationToken cancellationToken)
        {
            CurrentUser user = _currentUserAccessor.User;
            if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.Subject))
            {
                throw new InvalidOperationException("An authenticated identity is required to change an email address.");
            }

            return await _dbContext.MyssUserProfiles
                .SingleOrDefaultAsync(p => p.Subject == user.Subject, cancellationToken);
        }

        private void LogNoLinkBaseUrl(MyssUserProfile profile)
        {
            _logger.LogError(
                "No email verification link for profile {ProfileId}: Account:PinReset:LinkBaseUrl is not set "
                + "(deployed: Myss_Account__PinReset__LinkBaseUrl)",
                profile.Id);
        }

        private EmailMessage VerificationEmail(string to, Guid token)
        {
            Uri link = _links.BuildLink(EmailVerificationConfig.LinkPath, token);
            string lifetime = EmailedLinkToken.Describe(_config.TokenLifetime);
            string body =
                "Confirm that this is your email address for your My Self Serve account.\n\n"
                + $"[Confirm your email address]({link.AbsoluteUri})\n\n"
                + $"The link works once and expires in {lifetime}. "
                + "If it has expired, sign in to My Self Serve and choose Resend link on Account Info.\n\n"
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

        /// <summary>How a send went, as far as this side can tell (MYSS-326).</summary>
        private enum SendResult
        {
            /// <summary>Accepted by the email service.</summary>
            Sent,

            /// <summary>Certainly not sent: refused, or never reached the service.</summary>
            NotSent,

            /// <summary>The service refused the address itself; certainly not sent.</summary>
            AddressRejected,

            /// <summary>Failed in a way the service may have taken it anyway (a timeout).</summary>
            MayHaveBeenSent,
        }

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
