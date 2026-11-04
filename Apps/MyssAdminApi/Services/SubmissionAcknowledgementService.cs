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
    /// The only path that writes a confirmation message: validate, check the live
    /// message is still the one the edit started from, then add a new entry that
    /// records who saved it.
    /// </summary>
    public class SubmissionAcknowledgementService : ISubmissionAcknowledgementService
    {
        private const int HistoryLength = 20;

        private readonly ISubmissionAcknowledgementAdminProvider _provider;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<SubmissionAcknowledgementService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubmissionAcknowledgementService"/> class.
        /// </summary>
        /// <param name="provider">Injected confirmation message reader and writer.</param>
        /// <param name="currentUser">Injected caller identity, stored with each entry.</param>
        /// <param name="logger">Injected logger.</param>
        public SubmissionAcknowledgementService(
            ISubmissionAcknowledgementAdminProvider provider,
            ICurrentUserAccessor currentUser,
            ILogger<SubmissionAcknowledgementService> logger)
        {
            _provider = provider;
            _currentUser = currentUser;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<SubmissionAcknowledgementAdminModel> GetAsync(
            string formSpecId,
            CancellationToken cancellationToken)
        {
            return ToModel(await _provider.ReadNewestAsync(formSpecId, HistoryLength, cancellationToken));
        }

        /// <inheritdoc/>
        public async Task<SubmissionAcknowledgementSaveResultModel> SaveAsync(
            string formSpecId,
            SaveSubmissionAcknowledgementRequestModel request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyList<ValidationErrorModel> errors = SubmissionAcknowledgementValidator.Validate(request);
            if (errors.Count > 0)
            {
                return SubmissionAcknowledgementSaveResultModel.Refused(errors);
            }

            string? changedBy = _currentUser.User.IdirUsername;
            if (string.IsNullOrWhiteSpace(changedBy))
            {
                throw new InvalidOperationException("A confirmation message can only be saved by an IDIR user.");
            }

            var texts = new SubmissionAcknowledgementTexts(
                SubmissionAcknowledgementValidator.Normalise(request.Heading),
                SubmissionAcknowledgementValidator.Normalise(request.Title),
                SubmissionAcknowledgementValidator.Normalise(request.Body));
            IReadOnlyList<SubmissionAcknowledgementEntry> history;
            SubmissionAcknowledgementEntry saved;
            try
            {
                history = await _provider.ReadNewestAsync(formSpecId, HistoryLength, cancellationToken);
                string? liveId = history.Count > 0 ? history[0].Id : null;
                if (!string.Equals(liveId, request.BasedOn, StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "Confirmation message for {FormSpecId} not saved by {IdirUsername}: it changed after the edit began",
                        formSpecId,
                        changedBy);
                    return SubmissionAcknowledgementSaveResultModel.Changed(new ValidationErrorModel
                    {
                        Field = "basedOn",
                        Keyword = SubmissionAcknowledgementKeywords.Changed,
                        Message = "The confirmation message changed after you started editing.",
                    });
                }

                // Not cancelled with the editor's request: Strapi may already be committing the
                // write. The HTTP client's timeout still bounds it.
                saved = await _provider.CreateAsync(formSpecId, texts, changedBy, CancellationToken.None);
            }
            catch (Exception ex) when (ex is StrapiWriteException or ContentEngineUnavailableException)
            {
                // A timeout or an unreadable reply can follow a write Strapi completed, so the
                // attempt is recorded before the 502.
                _logger.LogWarning(
                    "Confirmation message for {FormSpecId} by {IdirUsername}: the save failed or its outcome is unknown",
                    formSpecId,
                    changedBy);
                throw;
            }

            _logger.LogInformation(
                "Confirmation message for {FormSpecId} published by {IdirUsername}",
                formSpecId,
                changedBy);
            return SubmissionAcknowledgementSaveResultModel.Accepted(
                ToModel([saved, .. history.Take(HistoryLength - 1)]));
        }

        private static SubmissionAcknowledgementAdminModel ToModel(IReadOnlyList<SubmissionAcknowledgementEntry> history) =>
            new() { Current = history.Count > 0 ? history[0] : null, History = history };
    }
}
