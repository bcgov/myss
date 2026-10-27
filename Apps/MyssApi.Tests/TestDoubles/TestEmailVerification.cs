namespace Myss.Api.Tests.TestDoubles
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Services;
    using Myss.Common.Auth;
    using Myss.Data;

    /// <summary>
    /// The real email verification service (MYSS-305) over a test context, with
    /// email recorded rather than sent, for tests of what registers through it.
    /// </summary>
    public static class TestEmailVerification
    {
        /// <summary>The webclient URL the test links are built on.</summary>
        public static readonly Uri LinkBaseUrl = new("https://myss.test/");

        /// <summary>Creates the service.</summary>
        /// <param name="db">The forms context the caller uses too.</param>
        /// <param name="caller">The signed-in caller.</param>
        /// <param name="email">Where sent email is recorded.</param>
        /// <param name="clock">The clock; the system clock when null.</param>
        /// <param name="config">Lifetime and cooldown; the defaults when null.</param>
        /// <returns>The service.</returns>
        public static EmailVerificationService NewService(
            FormsDbContext db,
            ICurrentUserAccessor caller,
            FakeEmailProvider email,
            TimeProvider? clock = null,
            EmailVerificationConfig? config = null) => new(
                NullLogger<EmailVerificationService>.Instance,
                db,
                caller,
                email,
                clock ?? TimeProvider.System,
                config ?? new EmailVerificationConfig(),
                new PinResetConfig { LinkBaseUrl = LinkBaseUrl },
                new FakeErrorMessageProvider());
    }
}
