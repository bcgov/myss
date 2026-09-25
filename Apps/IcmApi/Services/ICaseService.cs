namespace Icm.Api.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>ICM case lookup, authenticated on the caller's behalf.</summary>
    /// <remarks>
    /// <b>The thing to inject.</b> Finds a person's case from what a contact search
    /// returned — their row id, person id or integration id — or by case number, reads
    /// one case by its row id, and lists the people on a case with their relationship to
    /// it. Together with <see cref="IContactService"/> that is most of what the legacy
    /// INT-331 login call returned. Reach for <see cref="Repositories.ICaseRepository"/>
    /// instead only when the caller already holds a token of its own.
    /// </remarks>
    public interface ICaseService
    {
        /// <summary>Searches cases.</summary>
        /// <param name="query">The search. At least one criterion must be set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        /// <exception cref="System.ArgumentException">
        /// The query has no criterion, or one that is blank or not safe to search with.
        /// </exception>
        Task<CasePage> SearchAsync(CaseQuery query, CancellationToken cancellationToken = default);

        /// <summary>Gets one case by its row id.</summary>
        /// <param name="caseKey">The Siebel row id of the case — <see cref="Case.Id"/>, not the case number.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The case, or null when the key matches nothing the caller can see.</returns>
        Task<Case?> GetAsync(
            string caseKey,
            CaseReadOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the people on a case, with their relationship to it.</summary>
        /// <param name="caseKey">The Siebel row id of the case.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The people on the case; empty when there are none the caller can see.</returns>
        Task<IReadOnlyList<CaseContact>> GetContactsAsync(
            string caseKey,
            CaseReadOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
