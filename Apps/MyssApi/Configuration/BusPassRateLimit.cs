namespace Myss.Api.Configuration
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.RateLimiting;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.RateLimiting;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Models;

    /// <summary>
    /// The rate limit on the anonymous bus pass submit route: a fixed window per
    /// client address. Behind the OpenShift router the address is the one the
    /// forwarded-headers middleware restores, so <c>ForwardProxies</c> must be
    /// enabled there or every citizen shares the router's address.
    /// </summary>
    public static class BusPassRateLimit
    {
        /// <summary>The policy name the submit action opts into.</summary>
        public const string PolicyName = "bus-pass-submit";

        /// <summary>
        /// Checks the limit is usable. The limiter itself would only object when
        /// the first partition is created, which is the first submission, so a
        /// bad value would surface as a 500 to a citizen rather than at startup.
        /// </summary>
        /// <param name="config">The limit.</param>
        /// <exception cref="InvalidOperationException">A value is zero or negative.</exception>
        public static void Validate(BusPassRateLimitConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            if (config.PermitLimit < 1 || config.WindowSeconds < 1)
            {
                throw new InvalidOperationException(
                    "BusPass:SubmitRateLimit is not usable: PermitLimit and WindowSeconds must both be at least 1 "
                    + $"(PermitLimit={config.PermitLimit}, WindowSeconds={config.WindowSeconds}).");
            }
        }

        /// <summary>
        /// Configures the limiter: the policy, the 429 status, and a problem body
        /// carrying the keyword the frontend matches on.
        /// </summary>
        /// <param name="options">The limiter options.</param>
        /// <param name="config">The limit.</param>
        public static void Configure(RateLimiterOptions options, BusPassRateLimitConfig config)
        {
            ArgumentNullException.ThrowIfNull(options);
            Validate(config);

            TimeSpan window = TimeSpan.FromSeconds(config.WindowSeconds);

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = config.PermitLimit,
                    Window = window,
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)window.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        title = "Too many submissions from this address. Try again later.",
                        status = StatusCodes.Status429TooManyRequests,
                        keyword = BusPassErrorKeywords.RateLimited,
                    },
                    cancellationToken);
            };
        }
    }
}
