namespace Myss.Api.Services
{
    using Myss.Api.Domain;

    /// <summary>
    /// Turns a PIN into the salted hash stored in <c>taapcd_aae_passcode</c>,
    /// and checks a PIN against one. The PIN itself is never stored.
    /// </summary>
    public interface IPinHasher
    {
        /// <summary>
        /// Hashes a PIN with a fresh random salt.
        /// </summary>
        /// <param name="pin">The validated PIN.</param>
        /// <returns>The self-describing hash string: format marker, salt and hash together.</returns>
        string Hash(Pin pin);

        /// <summary>
        /// Checks a PIN against a stored hash.
        /// </summary>
        /// <param name="storedHash">What <see cref="Hash"/> returned when the PIN was set.</param>
        /// <param name="pin">The PIN to check.</param>
        /// <param name="upgradedHash">
        /// A new hash of the same PIN when it matched but was stored under older
        /// hashing parameters, to be saved in place of the old one; otherwise null.
        /// </param>
        /// <returns>True when the PIN matches.</returns>
        bool Verify(string storedHash, Pin pin, out string? upgradedHash);
    }
}
