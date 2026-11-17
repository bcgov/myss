namespace Myss.Api.Services
{
    using System;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// The single-use token behind every emailed link (RULE-IDA-02, BR-D7-09): a
    /// random GUID that exists only in the email, stored as its hash and nulled
    /// when used. Shared by the PIN reset (MYSS-36) and email verification
    /// (MYSS-305), so both links are made and checked the same way.
    /// </summary>
    internal static class EmailedLinkToken
    {
        /// <summary>
        /// Makes a new token. Random bytes from the CSPRNG rather than
        /// <see cref="Guid.NewGuid"/>, whose randomness source is a platform
        /// detail; the token is still a GUID.
        /// </summary>
        /// <returns>The token.</returns>
        public static Guid New() => new(RandomNumberGenerator.GetBytes(16));

        /// <summary>
        /// SHA-256 of the token's canonical text, lower-case hex. Unsalted on
        /// purpose: the token is 128 random bits, so there is no small space of
        /// values to precompute, and an anonymous link has only the token to find
        /// the profile by. A salted hash would need the salt from a row it has not
        /// found yet; this one is the key of the unique index on the hash column.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>64 hex characters.</returns>
        public static string Hash(Guid token) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token.ToString("D"))));

        /// <summary>
        /// Hashes a token as it came back from a link, whatever its case.
        /// </summary>
        /// <param name="raw">The token from the request.</param>
        /// <param name="hash">The hash, when the token is a GUID.</param>
        /// <returns>True when the token is a GUID.</returns>
        public static bool TryHash(string? raw, out string hash)
        {
            if (Guid.TryParse(raw, out Guid token))
            {
                hash = Hash(token);
                return true;
            }

            hash = string.Empty;
            return false;
        }

        /// <summary>A lifetime as an email states it: "90 minutes", "1 hour", "24 hours".</summary>
        /// <param name="lifetime">The link's lifetime.</param>
        /// <returns>The wording.</returns>
        public static string Describe(TimeSpan lifetime)
        {
            int minutes = (int)lifetime.TotalMinutes;
            if (minutes >= 60 && minutes % 60 == 0)
            {
                int hours = minutes / 60;
                return hours.ToString(CultureInfo.InvariantCulture) + (hours == 1 ? " hour" : " hours");
            }

            return minutes.ToString(CultureInfo.InvariantCulture) + (minutes == 1 ? " minute" : " minutes");
        }
    }
}
