namespace Icm.Api.Host.Services
{
    using System.Linq;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Resolves the correlation id from the HTTP context: the caller's
    /// <c>X-Request-ID</c> when it sent a usable one (MyssApi always does),
    /// otherwise the server's own trace identifier for the request.
    /// </summary>
    /// <remarks>
    /// The same rule as MyssApi's accessor: the inbound header is untrusted input
    /// that ends up in log lines and an outbound header, so it is accepted only
    /// when it is short and made of the characters an id is made of.
    /// </remarks>
    public class CorrelationIdAccessor : ICorrelationIdAccessor
    {
        /// <summary>The header the id travels in, inbound and outbound.</summary>
        public const string HeaderName = "X-Request-ID";

        /// <summary>The longest inbound id accepted; longer values are treated as absent.</summary>
        public const int MaxLength = 128;

        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationIdAccessor"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Injected HTTP context accessor.</param>
        public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public string? CorrelationId
        {
            get
            {
                HttpContext? context = _httpContextAccessor.HttpContext;
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
        /// Whether a caller-supplied id is safe to carry through.
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
