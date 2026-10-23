namespace Myss.AdminApi.Providers
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stands in for the case management system until accepted applications
    /// are promoted to it. Every promotion succeeds and nothing leaves the
    /// API, so an accepted application shows as Accepted.
    /// </summary>
    public class PlaceholderApplicationPromotionProvider : IApplicationPromotionProvider
    {
        /// <inheritdoc/>
        public Task<PromotionResult> PromoteAsync(PromotionRequest application, CancellationToken cancellationToken)
        {
            return Task.FromResult(PromotionResult.Success);
        }
    }
}
