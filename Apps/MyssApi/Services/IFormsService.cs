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
        /// <returns>
        /// The stored submission, or the validation failures that stopped it
        /// being stored. Nothing is persisted when the result is invalid.
        /// </returns>
        Task<FormSubmissionResultModel> SubmitAsync(string formSpecId, FormSubmissionRequestModel request, CancellationToken cancellationToken);

        /// <summary>
        /// Loads a submission together with the archived spec version that rendered it.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The submission, or null when unknown.</returns>
        Task<FormSubmissionResponseModel?> GetSubmissionAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Loads a BC Bus Pass submission only when it belongs to that form and can be rendered to PDF.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The submission when it is valid for PDF generation, otherwise null.</returns>
        Task<FormSubmissionResponseModel?> GetBusPassSubmissionForPdfAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Lists submissions for a form, newest first (metadata only).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The submission summaries.</returns>
        Task<IReadOnlyList<FormSubmissionSummaryModel>> ListSubmissionsAsync(string formSpecId, CancellationToken cancellationToken);
    }
}
