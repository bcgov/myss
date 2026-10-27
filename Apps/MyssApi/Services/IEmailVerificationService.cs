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
        /// Registration's half (MYSS-305): applies the address from the form to a
        /// profile being saved and, when it needs proving, issues its token. Saves
        /// nothing; the caller saves the profile, then calls
        /// <see cref="SendIssuedLinkAsync"/>.
        /// </summary>
        /// <param name="profile">The profile, new or existing, tracked by the caller's context.</param>
        /// <param name="email">The address from the form.</param>
        /// <returns>The token to send, or null when there is nothing to verify or no link can be built.</returns>
        Guid? ApplyRegistrationEmail(MyssUserProfile profile, string email);

        /// <summary>
        /// Emails the link for a token issued by <see cref="ApplyRegistrationEmail"/>
        /// once the profile is saved. A failure is logged, not thrown: the
        /// registration stands, and the citizen can resend from Account Info.
        /// </summary>
        /// <param name="profile">The saved profile.</param>
        /// <param name="token">The issued token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the email was accepted for sending.</returns>
        Task<bool> SendIssuedLinkAsync(MyssUserProfile profile, Guid token, CancellationToken cancellationToken);
    }
}
