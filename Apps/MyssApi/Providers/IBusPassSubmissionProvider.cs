namespace Myss.Api.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// The boundary to the ICM middleware for bus pass requests. Implementations
    /// speak the middleware's REST contract; nothing Siebel-shaped comes back
    /// through here.
    /// </summary>
    public interface IBusPassSubmissionProvider
    {
        /// <summary>
        /// Submits a bus pass request to the middleware.
        /// </summary>
        /// <param name="application">The request in business terms.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The middleware's outcome. A business rejection is a normal outcome
        /// carrying an error code, not an exception.
        /// </returns>
        /// <exception cref="IcmApiUnavailableException">
        /// The middleware could not be reached, did not answer in time, or
        /// answered with a failure status.
        /// </exception>
        Task<BusPassSubmissionOutcomeModel> SubmitAsync(
            BusPassApplicationModel application,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Raised when no outcome could be obtained from the ICM middleware. The
    /// request may or may not have reached ICM; callers must not assume either.
    /// </summary>
    public class IcmApiUnavailableException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IcmApiUnavailableException"/> class.
        /// </summary>
        public IcmApiUnavailableException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IcmApiUnavailableException"/> class.
        /// </summary>
        /// <param name="message">What went wrong.</param>
        public IcmApiUnavailableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IcmApiUnavailableException"/> class.
        /// </summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="innerException">The underlying failure.</param>
        public IcmApiUnavailableException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Gets the HTTP status the middleware answered with, when it answered at all.
        /// </summary>
        public int? StatusCode { get; init; }
    }
}
