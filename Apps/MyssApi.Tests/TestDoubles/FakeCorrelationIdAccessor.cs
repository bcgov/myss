namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Api.Services;

    /// <summary>
    /// Fake <see cref="ICorrelationIdAccessor"/> with a scripted id.
    /// </summary>
    public sealed class FakeCorrelationIdAccessor : ICorrelationIdAccessor
    {
        /// <summary>Initializes a new instance of the <see cref="FakeCorrelationIdAccessor"/> class.</summary>
        /// <param name="correlationId">The id to report; null means "no request".</param>
        public FakeCorrelationIdAccessor(string? correlationId)
        {
            CorrelationId = correlationId;
        }

        /// <inheritdoc/>
        public string? CorrelationId { get; set; }
    }
}
