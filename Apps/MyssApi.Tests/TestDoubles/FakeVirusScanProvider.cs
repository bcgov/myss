namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Api.Providers;

    /// <summary>
    /// Fake <see cref="IVirusScanProvider"/> that records what it scanned, so
    /// tests can check the scan actually happened.
    /// </summary>
    public sealed class FakeVirusScanProvider : IVirusScanProvider
    {
        /// <summary>
        /// Gets the byte payloads passed to <see cref="ScanAsync"/>.
        /// </summary>
        public List<byte[]> ScannedPayloads { get; } = [];

        /// <summary>
        /// Gets or sets the verdict to return.
        /// </summary>
        public VirusScanResult Result { get; set; } = new(IsClean: true);

        /// <summary>
        /// Gets or sets a value indicating whether the scanner should act as
        /// if it were down.
        /// </summary>
        public bool Unavailable { get; set; }

        /// <inheritdoc/>
        public async Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
        {
            if (Unavailable)
            {
                throw new VirusScanUnavailableException("Fake scanner is unavailable.");
            }

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            ScannedPayloads.Add(buffer.ToArray());
            return Result;
        }
    }

    /// <summary>
    /// Caller accessor that always reports the given subject.
    /// </summary>
    /// <param name="subject">The subject to report.</param>
    public sealed class StubCurrentUserAccessor(string subject) : Myss.Api.Services.ICurrentUserAccessor
    {
        /// <inheritdoc/>
        public Myss.Api.Models.CurrentUser User { get; } = new()
        {
            IsAuthenticated = true,
            Subject = subject,
        };
    }
}
