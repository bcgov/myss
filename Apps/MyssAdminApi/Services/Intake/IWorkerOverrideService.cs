namespace Myss.AdminApi.Intake
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Worker override (MYSS-321): a worker fills in the simplified Application
    /// for Assistance for a client who cannot submit their own. Drafts are open
    /// to every worker. The submit makes the client's worker override account
    /// and puts the application in the worker queue.
    /// </summary>
    public interface IWorkerOverrideService
    {
        /// <summary>Starts a draft on the latest published worker override form.</summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The draft, with the spec that renders it.</returns>
        Task<WorkerOverrideResultModel> CreateAsync(CancellationToken cancellationToken);

        /// <summary>Reads a worker override application, with the spec that renders it.</summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The application, or NotFound.</returns>
        Task<WorkerOverrideResultModel> GetAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Saves a draft's answers, complete or not.</summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The row version last read and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The saved draft, or why not.</returns>
        Task<WorkerOverrideResultModel> SaveAnswersAsync(
            Guid id,
            WorkerOverrideAnswersRequestModel request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Submits: validates the answers, makes (or updates) the client's
        /// worker override account, and records the submit with the worker as
        /// its actor, which puts it in the worker queue.
        /// </summary>
        /// <param name="id">The application identifier.</param>
        /// <param name="request">The row version last read and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The submitted application, or why not.</returns>
        Task<WorkerOverrideResultModel> SubmitAsync(
            Guid id,
            WorkerOverrideAnswersRequestModel request,
            CancellationToken cancellationToken);
    }
}
