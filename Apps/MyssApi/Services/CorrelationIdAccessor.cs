namespace Myss.Api.Services
{
    using System.Linq;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Resolves the correlation id from the HTTP context: the caller's
    /// <c>X-Request-ID</c> when it sent a usable one, otherwise the server's own
    /// trace identifier for the request.
    /// </summary>
    /// <remarks>
    /// The inbound header is untrusted input that ends up in log lines, database
    /// rows and an outbound header, so it is accepted only when it is short and
    /// made of the characters an id is made of. Anything else falls back to the
    /// trace identifier rather than being carried through.
    /// </remarks>
    public class CorrelationIdAccessor : ICorrelationIdAccessor
    {
        /// <summary>
        /// The header the id travels in, inbound and outbound: <c>X-Request-ID</c>,
        /// the name the middleware and ICM are expected to carry it under too.
        /// </summary>
        public const string HeaderName = "X-Request-ID";

        /// <summary>
        /// The longest inbound id accepted; longer values are treated as absent.
        /// </summary>
        public const int MaxLength = 128;

        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationIdAccessor"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Injected HTTP context accessor.</param>
        public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public string? CorrelationId
        {
            get
            {
                HttpContext? context = this.httpContextAccessor.HttpContext;
                if (context is null)
                {
                    return null;
                }

                string? supplied = context.Request.Headers[HeaderName]
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?
                    .Trim();

                return IsUsable(supplied) ? supplied : context.TraceIdentifier;
            }
        }

        /// <summary>
        /// Whether a caller-supplied id is safe to carry through: non-empty,
        /// within <see cref="MaxLength"/>, and made only of letters, digits and
        /// the punctuation ids use.
        /// </summary>
        /// <param name="value">The trimmed header value.</param>
        /// <returns>True when the value can be used as the request's id.</returns>
        public static bool IsUsable(string? value)
        {
            return !string.IsNullOrEmpty(value)
                && value.Length <= MaxLength
                && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
        }
    }
}
