namespace Icm.Api.Host.Services
{
    using System;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Raised when no outcome could be obtained from ICM. Carries the stable
    /// keyword and the HTTP status the caller should receive, so the controller
    /// maps it without inspecting library or transport exception types.
    /// </summary>
    public class IcmUpstreamException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IcmUpstreamException"/> class.
        /// </summary>
        public IcmUpstreamException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IcmUpstreamException"/> class.
        /// </summary>
        /// <param name="message">What went wrong.</param>
        public IcmUpstreamException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IcmUpstreamException"/> class.
        /// </summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="innerException">The underlying failure.</param>
        public IcmUpstreamException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IcmUpstreamException"/> class.
        /// </summary>
        /// <param name="keyword">The stable keyword for the failure.</param>
        /// <param name="statusCode">The HTTP status the caller should receive.</param>
        /// <param name="message">What went wrong, in words safe to return to the caller.</param>
        /// <param name="innerException">The underlying failure.</param>
        public IcmUpstreamException(string keyword, int statusCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Keyword = keyword;
            StatusCode = statusCode;
        }

        /// <summary>Gets the stable keyword for the failure.</summary>
        public string Keyword { get; } = Contracts.BusPassKeywords.UpstreamError;

        /// <summary>Gets the HTTP status the caller should receive.</summary>
        public int StatusCode { get; } = StatusCodes.Status502BadGateway;
    }
}
