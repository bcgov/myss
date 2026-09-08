namespace Icm.Api.Repositories
{
    using System;

    /// <summary>
    /// Thrown when the token endpoint returns success but the body is not a token that can
    /// be used — an empty or absent <c>access_token</c>, or a <c>token_type</c> that is
    /// something other than Bearer.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Refit.ApiException"/>, which means the request itself
    /// failed. This one means the request succeeded and the answer was unusable, which
    /// points at the authorization server or its configuration rather than at the
    /// credentials.
    /// </remarks>
    public class OAuthTokenException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="OAuthTokenException"/> class.</summary>
        public OAuthTokenException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="OAuthTokenException"/> class.</summary>
        /// <param name="message">The message.</param>
        public OAuthTokenException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="OAuthTokenException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public OAuthTokenException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
