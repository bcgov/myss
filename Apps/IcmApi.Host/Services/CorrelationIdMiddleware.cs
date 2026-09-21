namespace Icm.Api.Host.Services
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Serilog.Context;

    /// <summary>
    /// Echoes the request's correlation id on the response and pushes it onto the
    /// log context, so every line written while handling the request carries it.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ICorrelationIdAccessor _correlationIdAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware.</param>
        /// <param name="correlationIdAccessor">Injected correlation id accessor.</param>
        public CorrelationIdMiddleware(RequestDelegate next, ICorrelationIdAccessor correlationIdAccessor)
        {
            _next = next;
            _correlationIdAccessor = correlationIdAccessor;
        }

        /// <summary>
        /// Runs the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task that completes when the pipeline has run.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            string? correlationId = _correlationIdAccessor.CorrelationId;
            if (correlationId is null)
            {
                await _next(context);
                return;
            }

            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationIdAccessor.HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("RequestId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
