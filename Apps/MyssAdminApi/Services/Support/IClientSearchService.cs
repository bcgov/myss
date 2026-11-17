namespace Myss.AdminApi.Support
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Support View client search (MYSS-275): finds MySS client profiles so a
    /// worker can validate a client's personal information. The criteria combine
    /// (every one given must match). First name, last name and BCeID user name
    /// match on any part, ignoring case; the SIN matches only in full, so it can
    /// later be compared as a keyed hash without changing this contract.
    /// </summary>
    public interface IClientSearchService
    {
        /// <summary>Searches the client profiles.</summary>
        /// <param name="criteria">What to search by.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matches, or the keyword of why the criteria were refused.</returns>
        Task<ClientSearchOutcome> SearchAsync(ClientSearchCriteria criteria, CancellationToken cancellationToken);
    }
}
