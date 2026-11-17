namespace Myss.AdminApi.Providers
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Hands an accepted Income Assistance application on to the case
    /// management system: the promotion step that follows a worker's Accept.
    /// </summary>
    /// <remarks>
    /// Nothing receives applications yet, so
    /// <see cref="PlaceholderApplicationPromotionProvider"/> stands in.
    /// ADR-0001 reserves a workflow engine (Temporal) for the promotion saga;
    /// that, or a direct ICM call, is a new implementation of this interface
    /// and the review service does not change.
    /// </remarks>
    public interface IApplicationPromotionProvider
    {
        /// <summary>
        /// Promotes an accepted application.
        /// </summary>
        /// <param name="application">What was accepted.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether it was handed on, and why not when it was not.</returns>
        Task<PromotionResult> PromoteAsync(PromotionRequest application, CancellationToken cancellationToken);
    }

    /// <summary>
    /// An accepted application as the promotion step receives it.
    /// </summary>
    /// <param name="ApplicationId">The application id.</param>
    /// <param name="ReferenceNumber">The request number the applicant was given.</param>
    /// <param name="FormSpecId">The logical form identifier.</param>
    /// <param name="FormSpecVersion">The spec version the answers were submitted under.</param>
    /// <param name="Answers">The submitted answers.</param>
    public sealed record PromotionRequest(
        Guid ApplicationId,
        string ReferenceNumber,
        string FormSpecId,
        int FormSpecVersion,
        JsonElement Answers);

    /// <summary>
    /// How a promotion ended.
    /// </summary>
    /// <param name="Succeeded">Whether the application was handed on.</param>
    /// <param name="Reason">Why not, for the operator; never applicant data.</param>
    public sealed record PromotionResult(bool Succeeded, string? Reason)
    {
        /// <summary>Gets a success.</summary>
        public static PromotionResult Success { get; } = new(true, null);

        /// <summary>Builds a failure.</summary>
        /// <param name="reason">Why it failed.</param>
        /// <returns>The result.</returns>
        public static PromotionResult Failed(string reason)
        {
            return new(false, reason);
        }
    }
}
