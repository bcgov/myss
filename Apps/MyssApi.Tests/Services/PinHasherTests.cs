namespace Myss.Api.Tests.Services
{
    using Microsoft.AspNetCore.Identity;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="PinHasher"/>: what lands in <c>taapcd_aae_passcode</c>
    /// is a salted hash, never the PIN (MYSS-258 AC6).
    /// </summary>
    public class PinHasherTests
    {
        private readonly PinHasher _hasher = new();

        [Fact]
        public void TheHashDoesNotContainThePin()
        {
            string hash = _hasher.Hash(PinOf("4821"));

            Assert.DoesNotContain("4821", hash, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSamePinHashesDifferentlyEachTime_BecauseEachHashHasItsOwnSalt()
        {
            Assert.NotEqual(_hasher.Hash(PinOf("4821")), _hasher.Hash(PinOf("4821")));
        }

        [Fact]
        public void VerifiesTheRightPin()
        {
            string hash = _hasher.Hash(PinOf("4821"));

            Assert.True(_hasher.Verify(hash, PinOf("4821"), out string? upgraded));
            Assert.Null(upgraded);
        }

        [Fact]
        public void RefusesAWrongPin()
        {
            string hash = _hasher.Hash(PinOf("4821"));

            Assert.False(_hasher.Verify(hash, PinOf("4822"), out string? upgraded));
            Assert.Null(upgraded);
        }

        [Fact]
        public void HandsBackAStrongerHashForOneStoredUnderOlderParameters()
        {
            // A hash in the framework's older (V2) format, as a PIN set before a
            // framework upgrade would be stored.
            var legacy = new PasswordHasher<MyssUserProfile>(
                Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions
                {
                    CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2,
                }));
            string oldHash = legacy.HashPassword(null!, "4821");

            Assert.True(_hasher.Verify(oldHash, PinOf("4821"), out string? upgraded));
            Assert.NotNull(upgraded);
            Assert.True(_hasher.Verify(upgraded, PinOf("4821"), out string? again));
            Assert.Null(again);
        }

        private static Pin PinOf(string digits) => Pin.TryCreate(digits).Value!;
    }
}
