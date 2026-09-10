namespace Myss.Api.Providers
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// Write-side counterpart to <see cref="IFormSpecProvider"/>: the four
    /// operations the admin form editor (MYSS-209) needs, and nothing else.
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
        /// each version, for the editor's forms list.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>One <see cref="FormSummaryModel"/> per logical form.</returns>
        Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the spec to open in the editor: the in-progress draft when one
        /// exists, otherwise the latest published version as the starting point
        /// for a new draft.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The spec to edit, or null when the form does not exist.</returns>
        Task<FormSpecModel?> GetDraftAsync(string formSpecId, CancellationToken cancellationToken);

        /// <summary>
        /// Saves an edited spec as a draft without publishing it. Updates the
        /// in-progress draft when one exists, otherwise creates a new draft at
        /// the next version. The saved draft is not visible to citizens until
        /// <see cref="PublishAsync"/> releases it.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="spec">The edited Form.io specification JSON.</param>
        /// <param name="title">The human-readable title, or null to leave it unset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The saved draft, including the version it was stored under.</returns>
        Task<FormSpecModel> SaveDraftAsync(string formSpecId, JsonElement spec, string? title, CancellationToken cancellationToken);

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
