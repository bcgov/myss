namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
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

        /// <summary>
        /// Lists every form and its versions for the admin editor (MYSS-209).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>One summary per logical form.</returns>
        Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the spec to open in the admin editor: the in-progress draft, or
        /// the latest published version as the starting point for a new draft.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The spec to edit, or null when the form does not exist.</returns>
        Task<FormSpecModel?> GetDraftAsync(string formSpecId, CancellationToken cancellationToken);

        /// <summary>
        /// Validates the spec's structure, then saves it as a draft. Nothing is
        /// written when the structure check fails.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="spec">The edited Form.io specification JSON.</param>
        /// <param name="title">The human-readable title, or null to leave it unset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The saved draft, or the structural failures that stopped it.</returns>
        Task<FormSpecWriteResultModel<FormSpecModel>> SaveDraftAsync(string formSpecId, JsonElement spec, string? title, CancellationToken cancellationToken);

        /// <summary>
        /// Validates the in-progress draft, then publishes it as the next version.
        /// Nothing is written when the structure check fails or there is no draft.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The published version number, or the reasons it was refused.</returns>
        Task<FormSpecWriteResultModel<PublishResultModel>> PublishAsync(string formSpecId, CancellationToken cancellationToken);
    }
}
