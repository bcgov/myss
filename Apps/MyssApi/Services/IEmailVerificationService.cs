namespace Myss.Api.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;
    using Myss.Data;

    /// <summary>
    /// Proving a citizen's email address (MYSS-305): the address from
    /// registration, and any address they change to on Account Info. An address
    /// is only used once its emailed link has been followed; until then a change
    /// waits as pending and the address before it stays in use.
    /// </summary>
    public interface IEmailVerificationService
    {
        /// <summary>
        /// Sets the caller's new address as pending and emails it a link. An
        /// unverified address is replaced outright; the caller's own verified
        /// address cancels any pending change.
        /// </summary>
        /// <param name="request">The new address.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, ProfileRequired, Invalid, TooSoon or EmailNotSent.</returns>
        Task<EmailVerificationResultModel> RequestChangeAsync(ChangeEmailRequestModel request, CancellationToken cancellationToken);

        /// <summary>
        /// Emails a new link for the address waiting to be verified, which kills
        /// the earlier one.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, ProfileRequired, NothingToVerify, TooSoon or EmailNotSent.</returns>
        Task<EmailVerificationResultModel> ResendAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Drops a pending change, and its link, keeping the address in use.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok or ProfileRequired; Ok when nothing was pending, too.</returns>
        Task<EmailVerificationResultModel> CancelChangeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Follows an emailed link: the pending address becomes the one in use
        /// (and the one before it is told), or the address from registration is
        /// marked verified. Anonymous: the token is the credential.
        /// </summary>
        /// <param name="request">The token from the link.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok or LinkInvalid.</returns>
        Task<EmailVerificationResultModel> VerifyAsync(VerifyEmailRequestModel request, CancellationToken cancellationToken);

        /// <summary>
        /// Registration's Step 2 (MYSS-295): emails a link to prove the address
        /// the caller is registering with, before any profile exists. An address
        /// already proven in this registration is not sent another (AC6); a new
        /// one replaces the last and must be proven afresh.
        /// </summary>
        /// <param name="request">The address from Step 1.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, Invalid, TooSoon or EmailNotSent.</returns>
        Task<EmailVerificationResultModel> RequestRegistrationEmailAsync(
            ChangeEmailRequestModel request,
            CancellationToken cancellationToken);

        /// <summary>
        /// The address the caller's registration is proving, and whether it has
        /// been: what Step 2's Continue checks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The status; no address when none has been asked for.</returns>
        Task<RegistrationEmailModel> GetRegistrationEmailAsync(CancellationToken cancellationToken);

        /// <summary>
        /// When the caller proved <paramref name="email"/> in this registration,
        /// or null when they have not: registration cannot complete without it.
        /// </summary>
        /// <param name="email">The address on the submitted form.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>When it was proven, or null.</returns>
        Task<DateTimeOffset?> RegistrationEmailVerifiedAtAsync(string email, CancellationToken cancellationToken);

        /// <summary>
        /// Puts an address proven during registration on the profile being saved,
        /// as verified, and drops its registration record. Saves nothing; the
        /// caller saves the profile.
        /// </summary>
        /// <param name="profile">The profile, tracked by the caller's context.</param>
        /// <param name="email">The proven address.</param>
        /// <param name="verifiedAt">When it was proven.</param>
        void ApplyVerifiedRegistrationEmail(MyssUserProfile profile, string email, DateTimeOffset verifiedAt);
    }
}
