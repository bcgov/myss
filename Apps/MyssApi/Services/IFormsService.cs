namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;
    using Myss.FormSpec;

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
        /// Gets the error message catalogue: the citizen-facing wording for every
        /// stable error keyword, as the content engine currently publishes it,
        /// with the compiled defaults where it has no row or cannot be read.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The wording keyed by keyword; never null.</returns>
        Task<IReadOnlyDictionary<string, string>> GetErrorMessagesAsync(CancellationToken cancellationToken);

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
        /// Stores a submission with its version stamp, after the spec validation
        /// and a caller-supplied set of domain rules both pass.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="request">The submission payload.</param>
        /// <param name="domainRules">
        /// Rules the spec cannot express (cross-field checks, conditionally
        /// required fields), run on the answers after the spec validation. Their
        /// failures are reported together with the spec's, so the citizen sees
        /// everything at once.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The stored submission, or every validation failure that stopped it
        /// being stored. Nothing is persisted when the result is invalid.
        /// </returns>
        Task<FormSubmissionResultModel> SubmitAsync(
            string formSpecId,
            FormSubmissionRequestModel request,
            Func<JsonElement, IReadOnlyList<ValidationErrorModel>>? domainRules,
            CancellationToken cancellationToken);
    }
}
