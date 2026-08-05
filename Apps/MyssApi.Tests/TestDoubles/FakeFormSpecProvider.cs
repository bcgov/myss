namespace Myss.Api.Tests.TestDoubles
{
    using System.Text.Json;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Fake <see cref="IFormSpecProvider"/> that records calls, so tests can
    /// assert which lookup the service performed.
    /// </summary>
    public sealed class FakeFormSpecProvider : IFormSpecProvider
    {
        /// <summary>
        /// Gets the formSpecIds passed to <see cref="GetLatestAsync"/>.
        /// </summary>
        public List<string> LatestCalls { get; } = [];

        /// <summary>
        /// Gets the (formSpecId, version) pairs passed to <see cref="GetVersionAsync"/>.
        /// </summary>
        public List<(string FormSpecId, int Version)> VersionCalls { get; } = [];

        /// <summary>
        /// Gets or sets the result returned by <see cref="GetLatestAsync"/>.
        /// </summary>
        public FormSpecModel? LatestResult { get; set; }

        /// <summary>
        /// Gets or sets the result returned by <see cref="GetVersionAsync"/>.
        /// </summary>
        public FormSpecModel? VersionResult { get; set; }

        /// <summary>
        /// Builds a minimal spec model for test arrangements.
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="version">The spec version.</param>
        /// <returns>A spec model with an empty spec body.</returns>
        public static FormSpecModel Spec(string formSpecId, int version)
        {
            using JsonDocument doc = JsonDocument.Parse("""{"components":[]}""");
            return new FormSpecModel
            {
                FormSpecId = formSpecId,
                Version = version,
                Title = $"{formSpecId} v{version}",
                Spec = doc.RootElement.Clone(),
            };
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetLatestAsync(string formSpecId, CancellationToken cancellationToken)
        {
            LatestCalls.Add(formSpecId);
            return Task.FromResult(LatestResult);
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetVersionAsync(string formSpecId, int version, CancellationToken cancellationToken)
        {
            VersionCalls.Add((formSpecId, version));
            return Task.FromResult(VersionResult);
        }
    }
}
