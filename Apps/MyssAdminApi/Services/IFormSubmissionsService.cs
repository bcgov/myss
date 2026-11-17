namespace Myss.AdminApi.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.AdminApi.Models;

    /// <summary>
    /// The staff reads of stored form submissions: the list for a form, one
    /// submission with the spec version that rendered it, and the BC Bus Pass PDF.
    /// The submissions are written by MyssApi; this side only reads them.
    /// </summary>
    public interface IFormSubmissionsService
    {
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
        /// Generates the BC Bus Pass PDF for a stored submission when the submission is valid for that form.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The PDF bytes, or null when the submission is unknown or not a BC bus pass submission.</returns>
        Task<byte[]?> GetBusPassSubmissionPdfAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Lists submissions for a form, newest first (metadata only).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The submission summaries.</returns>
        Task<IReadOnlyList<FormSubmissionSummaryModel>> ListSubmissionsAsync(string formSpecId, CancellationToken cancellationToken);
    }
}
