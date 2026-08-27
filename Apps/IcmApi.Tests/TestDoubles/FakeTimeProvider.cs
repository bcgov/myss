namespace Icm.Api.Tests.TestDoubles
{
    /// <summary>
    /// A clock the test moves by hand, so token expiry can be exercised without waiting
    /// for it.
    /// </summary>
    internal sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset start) => _utcNow = start;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan interval) => _utcNow = _utcNow.Add(interval);
    }
}
