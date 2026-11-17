namespace Myss.AdminApi.Services
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.AdminApi.Models;
    using Myss.FormSpec;

    /// <summary>
    /// The admin form editor's service: list forms, read and save drafts, publish.
    /// </summary>
    public interface IFormSpecAdminService
    {
        /// <summary>
        /// Lists every form and its versions for admin form management.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>One summary per logical form.</returns>
        Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the current draft spec: the in-progress draft, or the latest
        /// published version as the baseline for a new draft.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current draft spec, or null when the form does not exist.</returns>
        Task<FormSpecModel?> GetDraftOrLatestPublishedAsync(string formSpecId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets one version of a form: its published entry, or its draft entry when
        /// that version has no published entry.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="version">The version number.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The version's spec, or null when the version does not exist.</returns>
        Task<FormSpecModel?> GetFormVersionAsync(string formSpecId, int version, CancellationToken cancellationToken);

        /// <summary>
        /// Saves an edited spec as a draft (not published). Its structure is validated
        /// first, so nothing is written and the failures are returned when the check fails.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="spec">The edited Form.io specification JSON.</param>
        /// <param name="title">The human-readable title, or null to leave it unset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The saved draft, or the structural failures that stopped it.</returns>
        Task<FormSpecWriteResultModel<FormSpecModel>> SaveDraftAsync(string formSpecId, JsonElement spec, string? title, CancellationToken cancellationToken);

        /// <summary>
        /// Saves a spec as exactly the given version, as a new draft, and never updates an
        /// existing draft. Accepted with the created draft; a conflict when that version
        /// already exists or the form has an unpublished draft; refused with the structural
        /// or lifecycle failures otherwise, including a version that skips one. An
        /// infrastructure failure propagates as an exception.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="version">The version to create.</param>
        /// <param name="spec">The Form.io specification JSON.</param>
        /// <param name="title">The human-readable title, or null to leave it unset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created draft, or the reasons it was refused.</returns>
        Task<FormSpecWriteResultModel<FormSpecModel>> CreateDraftVersionAsync(
            string formSpecId, int version, JsonElement spec, string? title, CancellationToken cancellationToken);

        /// <summary>
        /// Publishes the in-progress draft as the next version. The draft's structure is
        /// re-validated first, so nothing is published when the check fails or there is no draft.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The published version number, or the reasons it was refused.</returns>
        Task<FormSpecWriteResultModel<PublishResultModel>> PublishAsync(string formSpecId, CancellationToken cancellationToken);
    }
}
