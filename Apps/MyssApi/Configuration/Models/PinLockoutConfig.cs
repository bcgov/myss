namespace Myss.Api.Configuration.Models
{
    using System;

    /// <summary>
    /// How many wrong current PINs a citizen may give while changing it before
    /// Change PIN locks, and for how long, bound from <c>Account:PinLockout</c>
    /// (MYSS-258). Counted per account in the database, so a restart or a
    /// second instance does not hand out fresh attempts.
    /// </summary>
    public class PinLockoutConfig
    {
        /// <summary>Gets or sets how many wrong PINs in a row start a lockout.</summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>Gets or sets how long a lockout lasts, in minutes.</summary>
        public int LockoutMinutes { get; set; } = 15;

        /// <summary>Gets the lockout length.</summary>
        public TimeSpan Lockout => TimeSpan.FromMinutes(LockoutMinutes);

        /// <summary>
        /// Refuses a configuration that would never lock or never unlock.
        /// </summary>
        /// <param name="config">The bound configuration.</param>
        /// <exception cref="InvalidOperationException">When either value is below 1.</exception>
        public static void Validate(PinLockoutConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            if (config.MaxAttempts < 1 || config.LockoutMinutes < 1)
            {
                throw new InvalidOperationException(
                    "Account:PinLockout is not usable: MaxAttempts and LockoutMinutes must both be at least 1 "
                    + $"(got {config.MaxAttempts} and {config.LockoutMinutes}).");
            }
        }
    }
}
