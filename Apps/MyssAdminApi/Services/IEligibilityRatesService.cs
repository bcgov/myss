namespace Myss.AdminApi.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.AdminApi.Models;

    /// <summary>
    /// Reads and saves the eligibility rate table an admin edits.
    /// </summary>
    public interface IEligibilityRatesService
    {
        /// <summary>
        /// Gets the table citizens are served once their API's cache expires: the
        /// latest published table, or the compiled table when the content engine
        /// cannot be read.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The rate table; never null.</returns>
        Task<EligibilityRatesModel> GetAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Validates the table, then saves and publishes it as the table effective
        /// today in British Columbia (a second save the same day updates that table).
        /// </summary>
        /// <param name="request">The complete table.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The saved table, or every reason it was refused.</returns>
        /// <exception cref="Providers.StrapiWriteException">The content engine failed the write for a reason other than its rate rules.</exception>
        /// <exception cref="Providers.ContentEngineUnavailableException">The content engine could not be used.</exception>
        Task<EligibilityRatesWriteResultModel> SaveAsync(
            SaveEligibilityRatesRequestModel request,
            CancellationToken cancellationToken);
    }
}
