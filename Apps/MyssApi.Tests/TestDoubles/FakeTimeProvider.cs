namespace Myss.Api.Tests.TestDoubles
{
    /// <summary>
    /// A clock the test moves by hand, for token expiry and timestamps.
    /// </summary>
    public sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        /// <summary>Initializes a new instance of the <see cref="FakeTimeProvider"/> class.</summary>
        /// <param name="utcNow">The initial instant.</param>
        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _utcNow;

        /// <summary>Moves the clock forward.</summary>
        /// <param name="by">How far.</param>
        public void Advance(TimeSpan by) => _utcNow += by;
    }
}
