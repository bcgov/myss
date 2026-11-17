namespace Myss.Api.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>Provides the texts of a form message from the content engine.</summary>
    public interface IFormMessageProvider
    {
        /// <summary>Gets a form message's live texts.</summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="messageKey">The message's key within the form.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The message, or none, or unavailable; never null.</returns>
        Task<FormMessageLookup> GetLiveAsync(string formSpecId, string messageKey, CancellationToken cancellationToken);
    }
}
