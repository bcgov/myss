namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Api.Providers;

    /// <summary>
    /// A promotion step that answers what the test sets, or throws, and
    /// remembers what it was handed.
    /// </summary>
    public sealed class FakeApplicationPromotionProvider : IApplicationPromotionProvider
    {
        /// <summary>Gets or sets the answer every promotion gets.</summary>
        public PromotionResult Result { get; set; } = PromotionResult.Success;

        /// <summary>Gets or sets an exception to throw instead of answering.</summary>
        public Exception? Throws { get; set; }

        /// <summary>Gets the requests received, in order.</summary>
        public List<PromotionRequest> Requests { get; } = [];

        /// <summary>Gets the cancellation token each request came with.</summary>
        public List<CancellationToken> Tokens { get; } = [];

        /// <inheritdoc/>
        public Task<PromotionResult> PromoteAsync(PromotionRequest application, CancellationToken cancellationToken)
        {
            Requests.Add(application);
            Tokens.Add(cancellationToken);
            if (Throws is not null)
            {
                throw Throws;
            }

            return Task.FromResult(Result);
        }
    }
}
