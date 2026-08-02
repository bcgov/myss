namespace Myss.Api.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// Provides versioned form specifications from the content engine.
    /// </summary>
    public interface IFormSpecProvider
    {
        /// <summary>
        /// Gets the latest published version of a form spec.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The spec, or null when the form does not exist.</returns>
        Task<FormSpecModel?> GetLatestAsync(string formSpecId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a specific archived version of a form spec (historical rendering).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="version">The spec version to fetch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The spec, or null when that version does not exist.</returns>
        Task<FormSpecModel?> GetVersionAsync(string formSpecId, int version, CancellationToken cancellationToken);
    }
}
