namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Myss.Api.Domain;
    using Myss.Api.Models;

    /// <summary>
    /// The BC Bus Pass business rules the Form.io spec cannot express on its
    /// own. Carried over from the legacy form's second validation pass, and run
    /// server-side before anything is stored.
    /// </summary>
    /// <remarks>
    /// Two kinds of rule live here. Cross-field rules (a SIN or an account
    /// number, an email when email is the contact method, a plausible date of
    /// birth) have no Form.io equivalent. Conditionally required fields do, but
    /// <see cref="FormSpecValidator"/> exempts them, a known gap, so the ones
    /// that matter for a deliverable request are checked again here.
    /// </remarks>
    public static class BusPassRules
    {
        /// <summary>The youngest age the program accepts an applicant at.</summary>
        public const int MinimumAge = 16;

        /// <summary>
        /// Checks the answers against the bus pass rules.
        /// </summary>
        /// <param name="answers">The submitted answers, keyed by component key.</param>
        /// <param name="today">Today's date, for the age rules.</param>
        /// <returns>Every failure, in field order; empty when the answers pass.</returns>
        public static IReadOnlyList<ValidationErrorModel> Validate(JsonElement answers, DateOnly today)
        {
            var errors = new List<ValidationErrorModel>();

            ValidateRequestType(answers, errors);
            ValidateIdentifier(answers, errors);
            ValidateDateOfBirth(answers, today, errors);
            ValidateContact(answers, errors);
            ValidateMailingAddress(answers, errors);

            return errors;
        }

        private static void ValidateRequestType(JsonElement answers, List<ValidationErrorModel> errors)
        {
            string? category = BusPassAnswers.GetString(answers, BusPassAnswers.ApplicantCategory);

            if (category == "existing" && BusPassAnswers.GetString(answers, BusPassAnswers.ExistingClientReason) is null)
            {
                errors.Add(Required(BusPassAnswers.ExistingClientReason, "A selection is required"));
            }

            if (category == "new")
            {
                if (BusPassAnswers.GetString(answers, BusPassAnswers.EligibilityCategory) is null)
                {
                    errors.Add(Required(BusPassAnswers.EligibilityCategory, "A selection is required"));
                }

                if (BusPassAnswers.GetBool(answers, BusPassAnswers.EligibilityAcknowledged) != true)
                {
                    errors.Add(Required(BusPassAnswers.EligibilityAcknowledged, "Acknowledgement is required"));
                }
            }
        }

        private static void ValidateIdentifier(JsonElement answers, List<ValidationErrorModel> errors)
        {
            string? sin = BusPassAnswers.Digits(BusPassAnswers.GetString(answers, BusPassAnswers.SocialInsuranceNumber));
            string? account = BusPassAnswers.Digits(BusPassAnswers.GetString(answers, BusPassAnswers.BusPassAccountNumber));

            if (sin is null && account is null)
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = BusPassAnswers.SocialInsuranceNumber,
                    Keyword = BusPassErrorKeywords.IdentifierRequired,
                    Message = "A Social Insurance Number or bus pass account number is required",
                });
            }
        }

        private static void ValidateDateOfBirth(JsonElement answers, DateOnly today, List<ValidationErrorModel> errors)
        {
            if (!BusPassAnswers.TryGetDateOfBirth(answers, out DateOnly dateOfBirth))
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = BusPassAnswers.BirthDay,
                    Keyword = BusPassErrorKeywords.DateOfBirthInvalid,
                    Message = "Enter a valid date of birth",
                });
                return;
            }

            if (dateOfBirth > today)
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = BusPassAnswers.BirthYear,
                    Keyword = BusPassErrorKeywords.DateOfBirthInFuture,
                    Message = "The date of birth cannot be in the future",
                });
                return;
            }

            if (dateOfBirth > today.AddYears(-MinimumAge))
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = BusPassAnswers.BirthYear,
                    Keyword = BusPassErrorKeywords.Under16,
                    Message = $"You must be at least {MinimumAge}",
                });
            }
        }

        private static void ValidateContact(JsonElement answers, List<ValidationErrorModel> errors)
        {
            bool prefersEmail = BusPassAnswers.GetString(answers, BusPassAnswers.PreferredCommunication) == "email";
            if (prefersEmail && BusPassAnswers.GetString(answers, BusPassAnswers.Email) is null)
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = BusPassAnswers.Email,
                    Keyword = BusPassErrorKeywords.EmailRequiredForEmailContact,
                    Message = "An email address is required when email is the preferred means of communication",
                });
            }
        }

        private static void ValidateMailingAddress(JsonElement answers, List<ValidationErrorModel> errors)
        {
            if (BusPassAnswers.GetString(answers, BusPassAnswers.MailingAddressDifferent) != "yes")
            {
                return;
            }

            AddIfMissing(answers, errors, BusPassAnswers.MailingStreetAddress1, "An address is required");
            AddIfMissing(answers, errors, BusPassAnswers.MailingCity, "A city is required");
            AddIfMissing(answers, errors, BusPassAnswers.MailingProvince, "A province is required");
            AddIfMissing(answers, errors, BusPassAnswers.MailingPostalCode, "A postal code is required");
        }

        private static void AddIfMissing(JsonElement answers, List<ValidationErrorModel> errors, string key, string message)
        {
            if (BusPassAnswers.GetString(answers, key) is null)
            {
                errors.Add(Required(key, message));
            }
        }

        private static ValidationErrorModel Required(string field, string message)
        {
            return new ValidationErrorModel
            {
                Field = field,
                Keyword = ValidationKeywords.FieldRequired,
                Message = message,
            };
        }
    }
}
