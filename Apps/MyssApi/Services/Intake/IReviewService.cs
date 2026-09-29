namespace Myss.Api.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The worker side of Application Intake for the POC: every submitted
    /// application, read-only, and the three status moves. Any worker with an
    /// IDIR may act; there is no assignment.
    /// </summary>
    public interface IReviewService
    {
        /// <summary>
        /// Lists every application that has been submitted, newest submission first.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The summaries; drafts never appear.</returns>
        Task<IReadOnlyList<ReviewApplicationSummaryModel>> ListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Reads a submitted application with the archived spec that renders it.
        /// </summary>
        /// <param name="id">The application.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, or NotFound for an unknown id or a draft.</returns>
        Task<ReviewResultModel> GetAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Applies a worker action by appending its event.
        /// </summary>
        /// <param name="id">The application.</param>
        /// <param name="action">One of <see cref="WorkerActions"/>.</param>
        /// <param name="request">The stream version the worker last saw.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, NotFound, NotAllowed or StaleVersion.</returns>
        Task<ReviewResultModel> ActAsync(
            Guid id,
            string action,
            ReviewActionRequestModel request,
            CancellationToken cancellationToken);
    }
}
