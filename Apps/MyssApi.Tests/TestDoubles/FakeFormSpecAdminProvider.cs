namespace Myss.Api.Tests.TestDoubles
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Fake <see cref="IFormSpecAdminProvider"/> that records calls and returns
    /// canned results, so service tests can assert the validate-then-write
    /// behaviour without a real Strapi.
    /// </summary>
    public sealed class FakeFormSpecAdminProvider : IFormSpecAdminProvider
    {
        /// <summary>Gets the (formSpecId, title) pairs passed to <see cref="SaveDraftAsync"/>.</summary>
        public List<(string FormSpecId, string? Title)> SaveDraftCalls { get; } = [];

        /// <summary>Gets the formSpecIds passed to <see cref="PublishAsync"/>.</summary>
        public List<string> PublishCalls { get; } = [];

        /// <summary>Gets the formSpecIds passed to <see cref="GetDraftOrLatestPublishedAsync"/>.</summary>
        public List<string> GetDraftCalls { get; } = [];

        /// <summary>Gets the (formSpecId, version) pairs passed to <see cref="GetVersionAsync"/>.</summary>
        public List<(string FormSpecId, int Version)> GetVersionCalls { get; } = [];

        /// <summary>Gets the formSpecIds passed to <see cref="GetVersionStateAsync"/>.</summary>
        public List<string> VersionStateCalls { get; } = [];

        /// <summary>Gets the (formSpecId, version, title) triples passed to <see cref="CreateDraftVersionAsync"/>.</summary>
        public List<(string FormSpecId, int Version, string? Title)> CreateDraftVersionCalls { get; } = [];

        /// <summary>Gets or sets the forms returned by <see cref="ListFormsAsync"/>.</summary>
        public IReadOnlyList<FormSummaryModel> Forms { get; set; } = [];

        /// <summary>Gets or sets the spec returned by <see cref="GetDraftOrLatestPublishedAsync"/>.</summary>
        public FormSpecModel? Draft { get; set; }

        /// <summary>Gets or sets the spec returned by <see cref="SaveDraftAsync"/> (defaults to a spec built from the arguments).</summary>
        public FormSpecModel? Saved { get; set; }

        /// <summary>Gets the specs returned by <see cref="GetVersionAsync"/>, keyed by form and version.</summary>
        public Dictionary<(string FormSpecId, int Version), FormSpecModel> Versions { get; } = [];

        /// <summary>Gets or sets the state returned by <see cref="GetVersionStateAsync"/> (defaults to a form that does not exist).</summary>
        public FormSpecVersionState VersionState { get; set; } = new(LatestVersion: 0, InProgressDraftVersion: null);

        /// <summary>Gets or sets the version returned by <see cref="PublishAsync"/>.</summary>
        public int PublishedVersion { get; set; } = 1;

        /// <summary>Gets or sets an exception for <see cref="SaveDraftAsync"/> to throw.</summary>
        public Exception? SaveDraftException { get; set; }

        /// <summary>Gets or sets an exception for <see cref="PublishAsync"/> to throw.</summary>
        public Exception? PublishException { get; set; }

        /// <summary>Gets or sets an exception for <see cref="CreateDraftVersionAsync"/> to throw.</summary>
        public Exception? CreateDraftVersionException { get; set; }

        /// <inheritdoc/>
        public Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Forms);

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetDraftOrLatestPublishedAsync(string formSpecId, CancellationToken cancellationToken)
        {
            GetDraftCalls.Add(formSpecId);
            return Task.FromResult(Draft);
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetVersionAsync(string formSpecId, int version, CancellationToken cancellationToken)
        {
            GetVersionCalls.Add((formSpecId, version));
            return Task.FromResult(Versions.TryGetValue((formSpecId, version), out FormSpecModel? found) ? found : null);
        }

        /// <inheritdoc/>
        public Task<FormSpecVersionState> GetVersionStateAsync(string formSpecId, CancellationToken cancellationToken)
        {
            VersionStateCalls.Add(formSpecId);
            return Task.FromResult(VersionState);
        }

        /// <inheritdoc/>
        public Task<FormSpecModel> SaveDraftAsync(string formSpecId, JsonElement spec, string? title, CancellationToken cancellationToken)
        {
            SaveDraftCalls.Add((formSpecId, title));
            if (SaveDraftException is not null)
            {
                throw SaveDraftException;
            }

            return Task.FromResult(Saved ?? new FormSpecModel
            {
                FormSpecId = formSpecId,
                Version = PublishedVersion,
                Title = title,
                Spec = spec,
            });
        }

        /// <inheritdoc/>
        public Task<FormSpecModel> CreateDraftVersionAsync(
            string formSpecId, int version, JsonElement spec, string? title, CancellationToken cancellationToken)
        {
            CreateDraftVersionCalls.Add((formSpecId, version, title));
            if (CreateDraftVersionException is not null)
            {
                throw CreateDraftVersionException;
            }

            return Task.FromResult(new FormSpecModel
            {
                FormSpecId = formSpecId,
                Version = version,
                Title = title,
                Spec = spec,
            });
        }

        /// <inheritdoc/>
        public Task<int> PublishAsync(string formSpecId, CancellationToken cancellationToken)
        {
            PublishCalls.Add(formSpecId);
            if (PublishException is not null)
            {
                throw PublishException;
            }

            return Task.FromResult(PublishedVersion);
        }
    }
}
