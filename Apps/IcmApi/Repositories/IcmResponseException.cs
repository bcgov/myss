namespace Icm.Api.Repositories
{
    using System;

    /// <summary>
    /// Thrown when ICM reports success but the body does not say what the status code
    /// promised — a create that returns no record, for example.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Refit.ApiException"/>, which means the request failed. This
    /// one means it succeeded and the answer was not usable, which points at ICM or its
    /// configuration rather than at the request.
    /// </remarks>
    public class IcmResponseException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="IcmResponseException"/> class.</summary>
        public IcmResponseException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="IcmResponseException"/> class.</summary>
        /// <param name="message">The message.</param>
        public IcmResponseException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="IcmResponseException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public IcmResponseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
