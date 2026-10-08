namespace Myss.Api.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the error message catalogue from the content engine, with a
    /// compiled fallback: the citizen-facing wording for every stable error
    /// keyword.
    /// </summary>
    public interface IErrorMessageProvider
    {
        /// <summary>
        /// Gets the catalogue: the compiled defaults overlaid with every row
        /// the content engine currently publishes. Falls back to the defaults
        /// alone when the content engine cannot be read.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The wording keyed by keyword; never null, never empty.</returns>
        Task<IReadOnlyDictionary<string, string>> GetMessagesAsync(CancellationToken cancellationToken);
    }
}
