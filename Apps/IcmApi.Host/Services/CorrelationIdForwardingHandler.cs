namespace Icm.Api.Host.Services
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Puts the current request's correlation id on every outbound call to ICM,
    /// so the id MyssApi stamped on the citizen's action reaches the third system.
    /// </summary>
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
