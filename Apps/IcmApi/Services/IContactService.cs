namespace Icm.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>
    /// ICM contact search, authenticated on the caller's behalf.
    /// </summary>
    /// <remarks>
    /// <b>The thing to inject.</b> Finds contacts by any combination of the criteria on
    /// <see cref="ContactQuery"/> — the BC Services Card DID a citizen signed in with, or
    /// name, birth date and SIN when there is no card on file. Reach for
    /// <see cref="Repositories.IContactRepository"/> instead only when the caller already
    /// holds a token of its own.
    /// </remarks>
    public interface IContactService
    {
        /// <summary>Searches contacts.</summary>
        /// <param name="query">The search. At least one criterion must be set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The matching page, empty when nothing matched. A search meant to identify one
        /// person has done so only when it returns exactly one.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        /// The query has no criterion, or one that is blank or not safe to search with.
        /// </exception>
        Task<ContactPage> SearchAsync(
            ContactQuery query,
            CancellationToken cancellationToken = default);
    }
}
