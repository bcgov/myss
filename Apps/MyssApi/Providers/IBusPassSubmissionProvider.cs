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

        /// <summary>
        /// Gets the middleware's failure keyword (<c>ICM.BUSPASS.*</c>), when it
        /// answered with a problem body that carried one.
        /// </summary>
        public string? Keyword { get; init; }

        /// <summary>
        /// Gets a value indicating whether ICM may already hold the request. False
        /// only when the call provably never reached ICM: the middleware could not
        /// be connected to, or it said so itself (not configured, no token, ICM
        /// unreachable). True for a timeout, an upstream error, or anything
        /// unclassified, because a resend then risks a second service request.
        /// </summary>
        public bool MayHaveReachedIcm { get; init; } = true;
    }

    /// <summary>
    /// The failure keywords the ICM middleware answers with, as far as this API
    /// needs to tell them apart. They are the middleware's contract
    /// (<c>Apps/IcmApi.Host/Contracts/BusPassKeywords.cs</c>); only the three that
    /// prove the request never reached ICM are matched on here.
    /// </summary>
    public static class IcmApiKeywords
    {
        /// <summary>The middleware has no ICM credentials.</summary>
        public const string NotConfigured = "ICM.BUSPASS.NOT_CONFIGURED";

        /// <summary>The middleware could not obtain a token for ICM.</summary>
        public const string TokenUnavailable = "ICM.BUSPASS.TOKEN_UNAVAILABLE";

        /// <summary>The middleware could not connect to ICM.</summary>
        public const string Unreachable = "ICM.BUSPASS.UNREACHABLE";

        /// <summary>
        /// Whether a keyword proves the request never reached ICM.
        /// </summary>
        /// <param name="keyword">The middleware's keyword, or null when it sent none.</param>
        /// <returns>True when ICM cannot have the request.</returns>
        public static bool ProvesNotDelivered(string? keyword) =>
            keyword is NotConfigured or TokenUnavailable or Unreachable;
    }
}
