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

        /// <summary>Gets or sets the forms returned by <see cref="ListFormsAsync"/>.</summary>
        public IReadOnlyList<FormSummaryModel> Forms { get; set; } = [];

        /// <summary>Gets or sets the spec returned by <see cref="GetDraftAsync"/>.</summary>
        public FormSpecModel? Draft { get; set; }

        /// <summary>Gets or sets the spec returned by <see cref="SaveDraftAsync"/> (defaults to a spec built from the arguments).</summary>
        public FormSpecModel? Saved { get; set; }

        /// <summary>Gets or sets the version returned by <see cref="PublishAsync"/>.</summary>
        public int PublishedVersion { get; set; } = 1;

        /// <summary>Gets or sets an exception for <see cref="SaveDraftAsync"/> to throw.</summary>
        public Exception? SaveDraftException { get; set; }

        /// <summary>Gets or sets an exception for <see cref="PublishAsync"/> to throw.</summary>
        public Exception? PublishException { get; set; }

        /// <inheritdoc/>
        public Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Forms);

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetDraftAsync(string formSpecId, CancellationToken cancellationToken) =>
            Task.FromResult(Draft);

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
