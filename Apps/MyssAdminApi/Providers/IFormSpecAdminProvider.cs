namespace Myss.AdminApi.Providers
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.AdminApi.Models;
    using Myss.FormSpec;

    /// <summary>
    /// Write-side counterpart to MyssApi's <c>IFormSpecProvider</c>: the
    /// operations the brokered admin write path needs, and nothing else.
    /// Kept separate from the read interface on purpose - the citizen read path
    /// keeps using its read-only Strapi token, so adding write support never
    /// widens the privileges of the endpoints citizens hit. Implementations
    /// authenticate with the write-scoped <c>Strapi:AdminApiToken</c>, which is
    /// server-side only and never reaches the browser.
    /// </summary>
    public interface IFormSpecAdminProvider
    {
        /// <summary>
        /// Lists every form and its versions, with the published/draft state of
        /// each version, for the admin forms list.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>One <see cref="FormSummaryModel"/> per logical form.</returns>
        Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the current draft spec: the in-progress draft when one exists,
        /// otherwise the latest published version as the baseline for a new draft.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current draft spec, or null when the form does not exist.</returns>
        Task<FormSpecModel?> GetDraftOrLatestPublishedAsync(string formSpecId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets one version of a form: its published entry, or its draft entry
        /// when that version has no published entry.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="version">The version number.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The version's spec, or null when the version does not exist.</returns>
        Task<FormSpecModel?> GetVersionAsync(string formSpecId, int version, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the form's latest version and its in-progress draft (an unpublished
        /// draft above the latest published version), from one pair of reads.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The version state; a latest version of 0 when the form does not exist.</returns>
        Task<FormSpecVersionState> GetVersionStateAsync(string formSpecId, CancellationToken cancellationToken);

        /// <summary>
        /// Saves an edited spec as a draft without publishing it. Updates the
        /// in-progress draft when one exists, otherwise creates a new draft at
        /// the next version.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="spec">The edited Form.io specification JSON.</param>
        /// <param name="title">The human-readable title, or null to leave it unset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The saved draft, including the version it was stored under.</returns>
        Task<FormSpecModel> SaveDraftAsync(string formSpecId, JsonElement spec, string? title, CancellationToken cancellationToken);

        /// <summary>
        /// Creates the given version as a new draft entry. Unlike <see cref="SaveDraftAsync"/>,
        /// it always creates and never updates. Strapi refuses the write with a 400 carrying
        /// <c>FORMSPEC.VERSION.NOT_NEXT</c> when the version is not the highest existing
        /// version plus one. An existing unpublished draft is not by itself a refusal, so a
        /// caller that must not create a second draft checks for one first.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="version">The version to create.</param>
        /// <param name="spec">The Form.io specification JSON.</param>
        /// <param name="title">The human-readable title, or null to leave it unset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created draft.</returns>
        Task<FormSpecModel> CreateDraftVersionAsync(
            string formSpecId, int version, JsonElement spec, string? title, CancellationToken cancellationToken);

        /// <summary>
        /// Publishes the current in-progress draft as the next live version.
        /// Version sequencing and the immutability of published entries are
        /// enforced by Strapi's lifecycle, which this call does not bypass.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The version number that was published.</returns>
        Task<int> PublishAsync(string formSpecId, CancellationToken cancellationToken);
    }
}
