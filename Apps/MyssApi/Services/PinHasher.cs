namespace Myss.Api.Services
{
    using Microsoft.AspNetCore.Identity;
    using Myss.Api.Data;
    using Myss.Api.Domain;

    /// <summary>
    /// <see cref="IPinHasher"/> over ASP.NET Core Identity's
    /// <see cref="PasswordHasher{TUser}"/>: PBKDF2 with a 128-bit random salt
    /// per hash, the salt and parameters stored in the hash string itself.
    /// </summary>
    /// <remarks>
    /// Nothing outside MySS reads the PIN (MYSS-258), so the format is ours to
    /// choose, and this one is versioned: when the framework raises its
    /// defaults, <see cref="Verify"/> hands back an upgraded hash and the old
    /// one is replaced on the next successful check, without a migration.
    /// A 4-digit PIN has only 10,000 values, so the hash slows an offline
    /// guess down rather than preventing it; the attempt lockout is what
    /// protects a PIN online.
    /// </remarks>
    public sealed class PinHasher : IPinHasher
    {
        // The hasher ignores the user argument; the profile type only fixes TUser.
        private static readonly PasswordHasher<MyssUserProfile> Hasher = new();

        /// <inheritdoc/>
        public string Hash(Pin pin)
        {
            return Hasher.HashPassword(null!, pin.Digits);
        }

        /// <inheritdoc/>
        public bool Verify(string storedHash, Pin pin, out string? upgradedHash)
        {
            PasswordVerificationResult result = Hasher.VerifyHashedPassword(null!, storedHash, pin.Digits);
            upgradedHash = result == PasswordVerificationResult.SuccessRehashNeeded ? Hash(pin) : null;
            return result != PasswordVerificationResult.Failed;
        }
    }
}
