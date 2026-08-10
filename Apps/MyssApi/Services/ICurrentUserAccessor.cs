namespace Myss.Api.Services
{
    using Myss.Api.Models;

    /// <summary>
    /// Supplies the caller's typed identity for the current request.
    /// PERMANENT core: unchanged between Option 1 and Option 2.
    /// </summary>
    public interface ICurrentUserAccessor
    {
        /// <summary>
        /// Gets the current caller, or <see cref="CurrentUser.Anonymous"/> when the request is
        /// unauthenticated or there is no active HTTP context.
        /// </summary>
        CurrentUser User { get; }
    }
}
