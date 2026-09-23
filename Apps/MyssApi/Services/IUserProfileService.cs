namespace Myss.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads the registration-profile state for an authenticated identity.
    /// </summary>
    public interface IUserProfileService
    {
        /// <summary>
        /// Determines whether a profile exists for the supplied identity subject.
        /// </summary>
        /// <param name="subject">The stable authenticated identity subject.</param>
        /// <param name="cancellationToken">The request cancellation token.</param>
        /// <returns><see langword="true"/> when a MySS profile exists.</returns>
        Task<bool> HasProfileAsync(string subject, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the profile first name for the supplied identity subject.
        /// </summary>
        /// <param name="subject">The stable authenticated identity subject.</param>
        /// <param name="cancellationToken">The request cancellation token.</param>
        /// <returns>The stored first name, or <see langword="null"/> when no profile exists.</returns>
        Task<string?> GetFirstNameAsync(string subject, CancellationToken cancellationToken);
    }
}
