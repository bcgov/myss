namespace Myss.Api.Intake
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The applicant side of Application Intake: drafts, submits and reads of
    /// the caller's own applications. Every operation is scoped to the
    /// current user; an application another user owns does not exist as far
    /// as this service is concerned.
    /// </summary>
    public interface IIntakeService
    {
        /// <summary>
        /// Creates a new draft pinned to the latest published spec version.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, ProfileRequired or SpecUnavailable.</returns>
        Task<IntakeResultModel> CreateAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Lists the caller's applications, newest first.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The summaries.</returns>
        Task<IReadOnlyList<ApplicationSummaryModel>> ListMineAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Reads one of the caller's applications with the archived spec.
        /// </summary>
        /// <param name="id">The application.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok or NotFound.</returns>
        Task<IntakeResultModel> GetAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Saves the working copy of a draft.
        /// </summary>
        /// <param name="id">The application.</param>
        /// <param name="request">The row version last seen and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, NotFound, NotEditable, StaleVersion, Invalid or SpecUnavailable.</returns>
        Task<IntakeResultModel> SaveAnswersAsync(
            Guid id,
            ApplicationAnswersRequestModel request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Submits a draft: full validation, then one Submitted event.
        /// </summary>
        /// <param name="id">The application.</param>
        /// <param name="request">The row version last seen and the answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok, NotFound, NotEditable, StaleVersion, StreamConflict, Invalid or SpecUnavailable.</returns>
        Task<IntakeResultModel> SubmitAsync(
            Guid id,
            ApplicationAnswersRequestModel request,
            CancellationToken cancellationToken);
    }
}
