namespace Myss.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// Resetting a forgotten PIN by email (MYSS-36): the signed-in citizen asks
    /// for a link, and whoever holds the link sets a new PIN without the old one.
    /// </summary>
    public interface IPinResetService
    {
        /// <summary>
        /// Emails the caller a single-use reset link to their registered address,
        /// replacing any link sent before (RULE-IDA-02).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok with the expiry, ProfileRequired, PinNotAvailable or EmailNotSent.</returns>
        Task<PinResetResultModel> RequestAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sets a new PIN from an emailed link, and uses the link up. Anonymous:
        /// the token is the credential.
        /// </summary>
        /// <param name="request">The token and the new PIN twice.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, LinkInvalid or Invalid; nothing is saved unless all pass.</returns>
        Task<PinResetResultModel> ResetAsync(ResetPinRequestModel request, CancellationToken cancellationToken);
    }
}
