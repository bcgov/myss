namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;
    using Myss.Common.Auth;
    using Myss.Data;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Checks and records who a registration makes the account for. The rows it
    /// adds go into the same <see cref="FormsDbContext"/> as the new profile, so
    /// they are saved with it or not at all.
    /// </summary>
    public class AccountCreationService : IAccountCreationService
    {
        private readonly ILogger<AccountCreationService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly IAttachmentsService _attachments;
        private readonly ICurrentUserAccessor _currentUserAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountCreationService"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="attachments">The attachments module, which knows who uploaded each file.</param>
        /// <param name="currentUserAccessor">Injected current user accessor.</param>
        public AccountCreationService(
            ILogger<AccountCreationService> logger,
            FormsDbContext dbContext,
            IAttachmentsService attachments,
            ICurrentUserAccessor currentUserAccessor)
        {
            _logger = logger;
            _dbContext = dbContext;
            _attachments = attachments;
            _currentUserAccessor = currentUserAccessor;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ValidationErrorModel>> ValidateAsync(
            AccountCreationRequestModel? model,
            CancellationToken cancellationToken)
        {
            CurrentUser user = _currentUserAccessor.User;
            if (await _dbContext.MyssUserProfiles.AnyAsync(p => p.Subject == user.Subject, cancellationToken))
            {
                return [];
            }

            var errors = AccountCreationRules
                .Validate(model, signedInWithBceid: !string.IsNullOrWhiteSpace(user.BceidGuid))
                .ToList();
            if (model is null || !AccountCreationRules.TryParseContext(model.Context, out AccountCreationContext context))
            {
                return errors;
            }

            foreach (DocumentList documents in DocumentsFor(context, model))
            {
                if (errors.Exists(e => e.Field == documents.Field))
                {
                    continue;
                }

                if (!await CanLinkAsync(documents.Ids, user.Subject, cancellationToken))
                {
                    errors.Add(new ValidationErrorModel
                    {
                        Field = documents.Field,
                        Keyword = ValidationKeywords.RegistrationDocumentNotFound,
                        Message = "One of the attached documents could not be found. Remove it and attach it again.",
                    });
                }
            }

            return errors;
        }

        /// <inheritdoc/>
        public void Apply(MyssUserProfile profile, AccountCreationRequestModel model)
        {
            if (!AccountCreationRules.TryParseContext(model.Context, out AccountCreationContext context))
            {
                throw new InvalidOperationException("Account-creation answers must pass validation before they are recorded.");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            profile.AccountCreationContext = context;
            profile.HelperAuthorized = context == AccountCreationContext.WithHelper ? model.HelperAuthorized : null;

            if (AccountCreationRules.IsRepresentative(context))
            {
                AddRepresentative(profile, context, model.Representative!, now);
            }

            // Added explicitly, as the phones are: a row whose key is already
            // set would otherwise be taken for an existing one.
            int documentCount = 0;
            foreach (DocumentList documents in DocumentsFor(context, model))
            {
                foreach (Guid attachmentId in documents.Ids.Distinct())
                {
                    _dbContext.Add(new AccountDocument
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profile.Id,
                        AttachmentId = attachmentId,
                        Purpose = documents.Purpose,
                        AttachedAt = now,
                    });
                    documentCount++;
                }
            }

            _logger.LogInformation(
                "Recorded account-creation context {AccountCreationContext} for profile {ProfileId} with {DocumentCount} document(s)",
                context,
                profile.Id,
                documentCount);
        }

        private static IEnumerable<DocumentList> DocumentsFor(AccountCreationContext context, AccountCreationRequestModel model)
        {
            if (context == AccountCreationContext.WithHelper && model.HelperAuthorized == true)
            {
                yield return new DocumentList(
                    AccountCreationRules.ConsentDocumentsField,
                    AccountDocumentPurpose.ConsentToDisclosure,
                    model.ConsentDocumentIds ?? []);
            }

            if (context == AccountCreationContext.LegalRepresentative)
            {
                yield return new DocumentList(
                    AccountCreationRules.PowerOfAttorneyDocumentsField,
                    AccountDocumentPurpose.PowerOfAttorney,
                    model.PowerOfAttorneyDocumentIds ?? []);
            }
        }

        private void AddRepresentative(
            MyssUserProfile profile,
            AccountCreationContext context,
            RepresentativeRequestModel representative,
            DateTimeOffset now)
        {
            bool legalRepresentative = context == AccountCreationContext.LegalRepresentative;
            _dbContext.Add(new AccountRepresentative
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                Type = legalRepresentative ? RepresentativeType.LegalRepresentative : RepresentativeType.Parent,
                FirstName = representative.FirstName!.Trim(),
                LastName = representative.LastName!.Trim(),
                Phone = PhoneNumber.TryCreate(representative.Phone).Value!.Digits,
                Agency = legalRepresentative ? AccountCreationRules.TrimToNull(representative.Agency) : null,
                CreatedAt = now,
            });
        }

        /// <summary>
        /// Each file must be the caller's, released after a clean scan, and not
        /// linked to an account already.
        /// </summary>
        private async Task<bool> CanLinkAsync(IReadOnlyList<Guid> ids, string ownerSubject, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return true;
            }

            Guid[] distinct = ids.Distinct().ToArray();
            IReadOnlySet<Guid> released = await _attachments.FindReleasedAsync(distinct, ownerSubject, cancellationToken);
            if (released.Count != distinct.Length)
            {
                return false;
            }

            return !await _dbContext.AccountDocuments.AnyAsync(d => distinct.Contains(d.AttachmentId), cancellationToken);
        }

        private sealed record DocumentList(string Field, AccountDocumentPurpose Purpose, IReadOnlyList<Guid> Ids);
    }
}
