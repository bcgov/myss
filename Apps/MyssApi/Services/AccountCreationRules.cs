namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using Myss.Api.Models;
    using Myss.Data;
    using Myss.FormSpec;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// The checks on who a registration makes the account for and on that
    /// context's own answers. The web client applies the same rules, with the
    /// same fields, limits and wording, before anything is sent.
    /// </summary>
    /// <remarks>
    /// Whether each attached document exists, belongs to the caller and passed
    /// its scan needs the attachments table, so it is checked after these rules,
    /// by the account-creation service.
    /// </remarks>
    public static class AccountCreationRules
    {
        /// <summary>The error field for who the account is for.</summary>
        public const string ContextField = "accountContext";

        /// <summary>The error field for the helper's authorization.</summary>
        public const string HelperAuthorizedField = "helperAuthorized";

        /// <summary>The error field for the signed consent forms.</summary>
        public const string ConsentDocumentsField = "consentDocuments";

        /// <summary>The error field for the power of attorney documents.</summary>
        public const string PowerOfAttorneyDocumentsField = "powerOfAttorneyDocuments";

        /// <summary>The error field for the representative's first name.</summary>
        public const string RepresentativeFirstNameField = "representativeFirstName";

        /// <summary>The error field for the representative's last name.</summary>
        public const string RepresentativeLastNameField = "representativeLastName";

        /// <summary>The error field for the representative's phone number.</summary>
        public const string RepresentativePhoneField = "representativePhone";

        /// <summary>The error field for a legal representative's agency.</summary>
        public const string RepresentativeAgencyField = "representativeAgency";

        /// <summary>The longest name or agency, after trimming, as the profile's own names allow.</summary>
        public const int MaxTextLength = 200;

        /// <summary>The most documents of each kind a registration may attach.</summary>
        public const int MaxDocuments = 5;

        private static readonly Dictionary<string, AccountCreationContext> Contexts = new(StringComparer.Ordinal)
        {
            ["self"] = AccountCreationContext.Self,
            ["withHelper"] = AccountCreationContext.WithHelper,
            ["legalRepresentative"] = AccountCreationContext.LegalRepresentative,
            ["parentForMinor"] = AccountCreationContext.ParentForMinor,
        };

        /// <summary>
        /// Reads the context as the web client sends it.
        /// </summary>
        /// <param name="value">The context sent, such as <c>withHelper</c>.</param>
        /// <param name="context">The context, when it is one of the four.</param>
        /// <returns>True when the value names one of the four contexts.</returns>
        public static bool TryParseContext(string? value, out AccountCreationContext context) =>
            Contexts.TryGetValue(value ?? string.Empty, out context);

        /// <summary>
        /// Tells whether someone else makes the account for the person it is for.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>True for a legal representative or a parent.</returns>
        public static bool IsRepresentative(AccountCreationContext context) =>
            context is AccountCreationContext.LegalRepresentative or AccountCreationContext.ParentForMinor;

        /// <summary>
        /// Checks the account-creation answers of a registration that creates a profile.
        /// </summary>
        /// <param name="model">The answers sent, or null when none were.</param>
        /// <param name="signedInWithBceid">Whether the caller signed in with Basic BCeID.</param>
        /// <returns>Every failure; empty when the answers pass.</returns>
        public static IReadOnlyList<ValidationErrorModel> Validate(AccountCreationRequestModel? model, bool signedInWithBceid)
        {
            if (string.IsNullOrWhiteSpace(model?.Context))
            {
                return [Error(ContextField, ValidationKeywords.RegistrationContextRequired, "Select the option that describes you.")];
            }

            if (!TryParseContext(model.Context, out AccountCreationContext context))
            {
                return [Error(ContextField, ValidationKeywords.RegistrationContextUnknown, "Select one of the options shown.")];
            }

            var errors = new List<ValidationErrorModel>();
            if (IsRepresentative(context) && !signedInWithBceid)
            {
                errors.Add(Error(
                    ContextField,
                    ValidationKeywords.RegistrationContextSignInNotAllowed,
                    "To create an account for someone else, sign in with Basic BCeID."));
            }

            switch (context)
            {
                case AccountCreationContext.WithHelper:
                    ValidateHelper(model, errors);
                    break;
                case AccountCreationContext.LegalRepresentative:
                    ValidateRepresentative(model.Representative, asksForAgency: true, errors);
                    ValidateDocumentCount(model.PowerOfAttorneyDocumentIds, PowerOfAttorneyDocumentsField, errors);
                    break;
                case AccountCreationContext.ParentForMinor:
                    ValidateRepresentative(model.Representative, asksForAgency: false, errors);
                    break;
            }

            return errors;
        }

        /// <summary>
        /// Trims a representative's text answer.
        /// </summary>
        /// <param name="value">The answer as sent.</param>
        /// <returns>The trimmed answer, or null when it is blank.</returns>
        public static string? TrimToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static void ValidateHelper(AccountCreationRequestModel model, List<ValidationErrorModel> errors)
        {
            if (model.HelperAuthorized is null)
            {
                errors.Add(Error(
                    HelperAuthorizedField,
                    ValidationKeywords.RegistrationHelperAuthorizationRequired,
                    "Select whether we may share your information with this person."));
                return;
            }

            if (model.HelperAuthorized == false)
            {
                return;
            }

            if ((model.ConsentDocumentIds?.Count ?? 0) == 0)
            {
                errors.Add(Error(
                    ConsentDocumentsField,
                    ValidationKeywords.RegistrationDocumentConsentRequired,
                    "Attach your signed Consent to Disclosure of Information form."));
                return;
            }

            ValidateDocumentCount(model.ConsentDocumentIds, ConsentDocumentsField, errors);
        }

        private static void ValidateRepresentative(RepresentativeRequestModel? representative, bool asksForAgency, List<ValidationErrorModel> errors)
        {
            ValidateName(representative?.FirstName, RepresentativeFirstNameField, "Enter your first name.", errors);
            ValidateName(representative?.LastName, RepresentativeLastNameField, "Enter your last name.", errors);

            if (string.IsNullOrWhiteSpace(representative?.Phone))
            {
                errors.Add(Error(RepresentativePhoneField, ValidationKeywords.FieldRequired, "Enter your phone number."));
            }
            else if (PhoneNumber.TryCreate(representative.Phone) is { IsValid: false } phone)
            {
                errors.Add(Error(RepresentativePhoneField, phone.Keyword!, phone.Message!));
            }

            if (asksForAgency && TrimToNull(representative?.Agency) is { Length: > MaxTextLength })
            {
                errors.Add(TooLong(RepresentativeAgencyField));
            }
        }

        private static void ValidateName(string? value, string field, string requiredMessage, List<ValidationErrorModel> errors)
        {
            string? name = TrimToNull(value);
            if (name is null)
            {
                errors.Add(Error(field, ValidationKeywords.FieldRequired, requiredMessage));
            }
            else if (name.Length > MaxTextLength)
            {
                errors.Add(TooLong(field));
            }
        }

        private static void ValidateDocumentCount(IReadOnlyList<Guid>? ids, string field, List<ValidationErrorModel> errors)
        {
            if ((ids?.Count ?? 0) > MaxDocuments)
            {
                errors.Add(Error(field, ValidationKeywords.RegistrationDocumentTooMany, "Attach no more than 5 documents."));
            }
        }

        private static ValidationErrorModel TooLong(string field) =>
            Error(field, ValidationKeywords.FieldMaxLength, "This answer is too long.");

        private static ValidationErrorModel Error(string field, string keyword, string message) =>
            new() { Field = field, Keyword = keyword, Message = message };
    }
}
