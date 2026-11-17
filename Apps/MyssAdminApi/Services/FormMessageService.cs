namespace Myss.AdminApi.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Myss.AdminApi.Domain;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.Common.Auth;
    using Myss.Common.Content;
    using Myss.FormSpec;

    /// <summary>
    /// The form message editor, and the only path that writes a form message: validate,
    /// check the live message is still the one the edit started from, then add a new
    /// entry that records who saved it.
    /// </summary>
    public class FormMessageService : IFormMessageService
    {
        private const int HistoryLength = 20;

        // As in MyssApi: the live entry is the newest complete one, so a few are read.
        private const int LatestEntriesRead = 5;

        private readonly IFormMessageAdminProvider _provider;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<FormMessageService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormMessageService"/> class.
        /// </summary>
        /// <param name="provider">Injected form message reader and writer.</param>
        /// <param name="currentUser">Injected caller identity, stored with each entry.</param>
        /// <param name="logger">Injected logger.</param>
        public FormMessageService(
            IFormMessageAdminProvider provider,
            ICurrentUserAccessor currentUser,
            ILogger<FormMessageService> logger)
        {
            _provider = provider;
            _currentUser = currentUser;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<FormMessageSummaryModel>> ListAsync(CancellationToken cancellationToken)
        {
            var summaries = new List<FormMessageSummaryModel>();
            foreach (FormMessageDefinition definition in FormMessageDefinitions.All)
            {
                IReadOnlyList<FormMessageEntry> entries = await _provider.ReadNewestAsync(
                    definition.FormSpecId, definition.MessageKey, LatestEntriesRead, cancellationToken);
                FormMessageEntry? live = entries.Count > 0 ? entries[0] : null;
                summaries.Add(new FormMessageSummaryModel
                {
                    Definition = definition,
                    LastChangedAt = live?.ChangedAt,
                    LastChangedBy = live?.ChangedBy,
                });
            }

            return summaries;
        }

        /// <inheritdoc/>
        public async Task<FormMessageAdminModel> GetAsync(
            FormMessageDefinition definition,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return ToModel(
                definition,
                await _provider.ReadNewestAsync(definition.FormSpecId, definition.MessageKey, HistoryLength, cancellationToken));
        }

        /// <inheritdoc/>
        public async Task<FormMessageSaveResultModel> SaveAsync(
            FormMessageDefinition definition,
            SaveFormMessageRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyList<ValidationErrorModel> errors = FormMessageValidator.Validate(definition, request);
            if (errors.Count > 0)
            {
                return FormMessageSaveResultModel.Refused(errors);
            }

            string? changedBy = _currentUser.User.IdirUsername;
            if (string.IsNullOrWhiteSpace(changedBy))
            {
                throw new InvalidOperationException("A form message can only be saved by an IDIR user.");
            }

            var texts = new FormMessageTexts(
                FormMessageValidator.Normalise(request.Heading),
                FormMessageValidator.Normalise(request.Title),
                FormMessageValidator.Normalise(request.Body));
            IReadOnlyList<FormMessageEntry> history;
            FormMessageEntry saved;
            try
            {
                history = await _provider.ReadNewestAsync(
                    definition.FormSpecId, definition.MessageKey, HistoryLength, cancellationToken);
                string? liveId = history.Count > 0 ? history[0].Id : null;
                if (!string.Equals(liveId, request.BasedOn, StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "Form message {MessageKey} for {FormSpecId} not saved by {IdirUsername}: it changed after the edit began",
                        definition.MessageKey,
                        definition.FormSpecId,
                        changedBy);
                    return FormMessageSaveResultModel.Changed(new ValidationErrorModel
                    {
                        Field = "basedOn",
                        Keyword = FormMessageKeywords.Changed,
                        Message = "This message changed after you started editing.",
                    });
                }

                // Not cancelled with the editor's request: Strapi may already be committing the
                // write. The HTTP client's timeout still bounds it.
                saved = await _provider.CreateAsync(
                    definition.FormSpecId, definition.MessageKey, texts, changedBy, CancellationToken.None);
            }
            catch (Exception ex) when (ex is StrapiWriteException or ContentEngineUnavailableException)
            {
                // A timeout or an unreadable reply can follow a write Strapi completed, so the
                // attempt is recorded before the 502.
                _logger.LogWarning(
                    "Form message {MessageKey} for {FormSpecId} by {IdirUsername}: the save failed or its outcome is unknown",
                    definition.MessageKey,
                    definition.FormSpecId,
                    changedBy);
                throw;
            }

            _logger.LogInformation(
                "Form message {MessageKey} for {FormSpecId} published by {IdirUsername}",
                definition.MessageKey,
                definition.FormSpecId,
                changedBy);
            return FormMessageSaveResultModel.Accepted(
                ToModel(definition, [saved, .. history.Take(HistoryLength - 1)]));
        }

        private static FormMessageAdminModel ToModel(
            FormMessageDefinition definition,
            IReadOnlyList<FormMessageEntry> history) =>
            new() { Definition = definition, Current = history.Count > 0 ? history[0] : null, History = history };
    }
}
