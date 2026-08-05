namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// The forms module service: spec retrieval and submission lifecycle.
    /// </summary>
    public interface IFormsService
    {
        /// <summary>
        /// Gets the latest published spec for a form.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The spec, or null when unknown.</returns>
        Task<FormSpecModel?> GetLatestSpecAsync(string formSpecId, CancellationToken cancellationToken);

        /// <summary>
        /// Stores a submission with its version stamp.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="request">The submission payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored submission.</returns>
        Task<FormSubmissionResponseModel> SubmitAsync(string formSpecId, FormSubmissionRequestModel request, CancellationToken cancellationToken);

        /// <summary>
        /// Loads a submission together with the archived spec version that rendered it.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The submission, or null when unknown.</returns>
        Task<FormSubmissionResponseModel?> GetSubmissionAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Lists submissions for a form, newest first (metadata only).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The submission summaries.</returns>
        Task<IReadOnlyList<FormSubmissionSummaryModel>> ListSubmissionsAsync(string formSpecId, CancellationToken cancellationToken);
    }
}
