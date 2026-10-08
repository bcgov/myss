namespace Myss.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// The caller's Account Info (MYSS-271): reads it, and saves the parts the
    /// citizen may change. Every operation is scoped to the authenticated caller.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Gets the caller's account.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok or ProfileRequired.</returns>
        Task<AccountResultModel> GetAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Replaces the caller's phone numbers with the list given.
        /// </summary>
        /// <param name="request">The whole list as the citizen left it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, ProfileRequired or Invalid; nothing is saved unless every number passes.</returns>
        Task<AccountResultModel> UpdatePhonesAsync(UpdatePhonesRequestModel request, CancellationToken cancellationToken);

        /// <summary>
        /// Saves the caller's notification preference.
        /// </summary>
        /// <param name="request">The preference.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok or ProfileRequired.</returns>
        Task<AccountResultModel> UpdateNotificationPreferencesAsync(
            UpdateNotificationPreferencesRequestModel request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Changes the caller's PIN, or creates it when they have none (MYSS-258).
        /// A change needs the current PIN; wrong ones in a row lock it for a while.
        /// </summary>
        /// <param name="request">The current PIN, when there is one, and the new one twice.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, ProfileRequired, PinNotAvailable, PinLocked or Invalid; nothing is saved unless all pass.</returns>
        Task<AccountResultModel> SavePinAsync(SavePinRequestModel request, CancellationToken cancellationToken);
    }
}
