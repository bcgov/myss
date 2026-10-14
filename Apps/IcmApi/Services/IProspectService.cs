namespace Icm.Api.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;

    /// <summary>ICM prospects — the applicant rows on a service request — authenticated on the caller's behalf.</summary>
    /// <remarks>
    /// <b>The thing to inject.</b> Reads the prospects the bus pass workflow filed on a
    /// service request, and creates one directly — the half of "file a bus pass
    /// application without the workflow" that this component covers; the other half is
    /// <see cref="IServiceRequestService"/>'s create. Reach for
    /// <see cref="Repositories.IProspectRepository"/> instead only when the caller already
    /// holds a token of its own.
    /// </remarks>
    public interface IProspectService
    {
        /// <summary>Searches prospects on the standalone path.</summary>
        /// <param name="query">The search. At least one criterion must be set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching page, empty when nothing matched.</returns>
        /// <exception cref="System.ArgumentException">
        /// The query has no criterion, or one that is blank or not safe to search with.
        /// </exception>
        Task<ProspectPage> SearchAsync(ProspectQuery query, CancellationToken cancellationToken = default);

        /// <summary>Gets one prospect by its row id.</summary>
        /// <param name="prospectKey">The Siebel row id of the prospect — <see cref="Prospect.Id"/>.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The prospect, or null when the key matches nothing the caller can see.</returns>
        Task<Prospect?> GetAsync(
            string prospectKey,
            ProspectReadOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the prospects on a service request.</summary>
        /// <param name="serviceRequestKey">The Siebel row id of the service request.</param>
        /// <param name="options">Visibility, or null for the defaults.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Every prospect on the request; empty when there are none the caller can see.</returns>
        Task<IReadOnlyList<Prospect>> GetForServiceRequestAsync(
            string serviceRequestKey,
            ProspectReadOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Creates a prospect, linked by <see cref="ProspectInput.ServiceRequestId"/>.</summary>
        /// <param name="input">The fields to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created prospect as ICM stored it.</returns>
        /// <exception cref="System.ArgumentException">A required field or the service request id is missing.</exception>
        Task<Prospect> CreateAsync(ProspectInput input, CancellationToken cancellationToken = default);
    }
}
