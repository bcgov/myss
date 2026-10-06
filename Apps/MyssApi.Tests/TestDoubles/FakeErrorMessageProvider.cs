namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Api.Data;
    using Myss.Api.Providers;

    /// <summary>
    /// Fake <see cref="IErrorMessageProvider"/>: serves the compiled defaults
    /// overlaid with whatever a test puts in <see cref="Overrides"/>, and counts
    /// calls so a test can assert the catalogue is read only when a submission
    /// is refused.
    /// </summary>
    public sealed class FakeErrorMessageProvider : IErrorMessageProvider
    {
        /// <summary>Gets the rows a test publishes on top of the defaults.</summary>
        public Dictionary<string, string> Overrides { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the number of times the catalogue was read.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public Task<IReadOnlyDictionary<string, string>> GetMessagesAsync(CancellationToken cancellationToken)
        {
            Calls++;
            var catalogue = new Dictionary<string, string>(ErrorMessageDefaults.Messages, StringComparer.Ordinal);
            foreach ((string keyword, string message) in Overrides)
            {
                catalogue[keyword] = message;
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(catalogue);
        }
    }
}
