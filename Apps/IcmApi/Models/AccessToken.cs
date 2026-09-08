namespace Icm.Api.Models
{
    using System;

    /// <summary>An access token and how long it is good for.</summary>
    /// <remarks>
    /// What <c>IOAuthTokenRepository</c> returns. <c>IOAuthTokenService</c> is what most
    /// callers want instead — it hands back the token string and deals with the lifetime by
    /// caching against it.
    /// </remarks>
    public class AccessToken
    {
        /// <summary>Initializes a new instance of the <see cref="AccessToken"/> class.</summary>
        /// <param name="value">The token.</param>
        /// <param name="lifetime">
        /// How long the token is valid from the moment it was issued.
        /// <see cref="TimeSpan.Zero"/> when the authorization server did not say — RFC 6749
        /// makes <c>expires_in</c> optional — which means it cannot safely be cached.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
        public AccessToken(string value, TimeSpan lifetime)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            Value = value;
            Lifetime = lifetime < TimeSpan.Zero ? TimeSpan.Zero : lifetime;
        }

        /// <summary>Gets the token, to be sent as a bearer credential.</summary>
        public string Value { get; }

        /// <summary>
        /// Gets how long the token is valid from issue, or <see cref="TimeSpan.Zero"/> when
        /// the authorization server did not say.
        /// </summary>
        public TimeSpan Lifetime { get; }
    }
}
