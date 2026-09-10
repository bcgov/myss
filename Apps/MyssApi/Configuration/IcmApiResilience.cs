namespace Myss.Api.Configuration
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Http.Resilience;
    using Myss.Api.Configuration.Models;
    using Polly;

    /// <summary>
    /// The resilience envelope for calls to the ICM middleware: the retry,
    /// circuit breaker and timeouts the handbook's Siebel adapter section asks
    /// the MySS side to own (Part 4.2).
    /// </summary>
    public static class IcmApiResilience
    {
        /// <summary>
        /// How many times a request that never left the process is re-sent.
        /// </summary>
        public const int MaxRetryAttempts = 2;

        /// <summary>
        /// Headroom added to the total timeout so the last attempt is not cut off
        /// by the overall budget.
        /// </summary>
        public static readonly TimeSpan TotalTimeoutMargin = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Configures the standard resilience handler for the middleware client.
        /// </summary>
        /// <remarks>
        /// The bus pass POST is not idempotent upstream: ICM files a service
        /// request on every call, rejected ones included, and offers no
        /// idempotency key. So a retry is only safe when the request provably
        /// never reached the middleware — name resolution and connection
        /// failures. A timeout or a 5xx is not retried here; the citizen gets a
        /// clear failure and the submission stays stored for a later attempt.
        /// </remarks>
        /// <param name="options">The handler options to configure.</param>
        /// <param name="config">The middleware settings.</param>
        public static void Configure(HttpStandardResilienceOptions options, IcmApiConfig config)
        {
            TimeSpan attempt = TimeSpan.FromSeconds(config.TimeoutSeconds);

            options.AttemptTimeout.Timeout = attempt;
            options.TotalRequestTimeout.Timeout = (attempt * (MaxRetryAttempts + 1)) + TotalTimeoutMargin;

            options.Retry.MaxRetryAttempts = MaxRetryAttempts;
            options.Retry.ShouldHandle = args => ValueTask.FromResult(IsConnectFailure(args.Outcome));

            // The standard handler requires the sampling window to be at least
            // twice the attempt timeout.
            options.CircuitBreaker.SamplingDuration = attempt * 2;
        }

        /// <summary>
        /// Whether an outcome is a failure that happened before any bytes reached
        /// the middleware, which is the only kind safe to retry.
        /// </summary>
        /// <param name="outcome">The attempt outcome.</param>
        /// <returns>True when the request never left the process.</returns>
        public static bool IsConnectFailure(Outcome<HttpResponseMessage> outcome)
        {
            return outcome.Exception is HttpRequestException
            {
                HttpRequestError: HttpRequestError.NameResolutionError
                    or HttpRequestError.ConnectionError
                    or HttpRequestError.SecureConnectionError,
            };
        }
    }
}
