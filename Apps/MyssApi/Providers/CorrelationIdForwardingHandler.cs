namespace Myss.Api.Providers
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Services;

    /// <summary>
    /// Puts the current request's correlation id on every outbound call, so the
    /// middleware and ICM see the same <c>X-Request-ID</c> the citizen's request
    /// carried, and one action can be followed end to end across the three systems.
    /// </summary>
    /// <remarks>
    /// Registered outside the resilience handler, so the header is set once and
    /// every retry of the same request carries the same id.
    /// </remarks>
    public class CorrelationIdForwardingHandler : DelegatingHandler
    {
        private readonly ICorrelationIdAccessor _correlationIdAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationIdForwardingHandler"/> class.
        /// </summary>
        /// <param name="correlationIdAccessor">Injected correlation id accessor.</param>
        public CorrelationIdForwardingHandler(ICorrelationIdAccessor correlationIdAccessor)
        {
            _correlationIdAccessor = correlationIdAccessor;
        }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? correlationId = _correlationIdAccessor.CorrelationId;
            if (correlationId is not null && !request.Headers.Contains(CorrelationIdAccessor.HeaderName))
            {
                request.Headers.TryAddWithoutValidation(CorrelationIdAccessor.HeaderName, correlationId);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
