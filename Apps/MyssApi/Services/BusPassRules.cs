namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Myss.Api.Domain;
    using Myss.Api.Models;

    /// <summary>
    /// The BC Bus Pass business rules the Form.io spec cannot express on its
    /// own. Carried over from the legacy form's second validation pass, and run
    /// server-side before anything is stored.
    /// </summary>
    /// <remarks>
    /// Cross-field rules and the bus-pass-specific phone format live here.
    /// The phone format is also authored on v7 of the spec for browser feedback;
    /// the service enforces it even for an older or later spec version.
    /// </remarks>
    public static class BusPassRules
    {
        /// <summary>The youngest age the program accepts an applicant at.</summary>
        public const int MinimumAge = 16;

        /// <summary>
        /// The first bus pass form version whose spec names the email verification
        /// field's partner (<c>properties.myssMatches</c>), so the spec validator
        /// reports a mismatch and this class must not report it twice.
        /// </summary>
        public const int SpecValidatesEmailMatchVersion = 6;

        private static readonly Regex LegacyPhonePattern = new(
            @"^\(?([2-9][0-9][0-9])\)?[\s.-]?([0-9]{3})[\s.-]?([0-9]{4})$",
            RegexOptions.ECMAScript,
            TimeSpan.FromMilliseconds(100));

        /// <summary>
        /// Checks the answers against the bus pass rules.
        /// </summary>
        /// <param name="answers">The submitted answers, keyed by component key.</param>
        /// <param name="today">Today's date, for the age rules.</param>
        /// <param name="formSpecVersion">The spec version the answers were rendered with.</param>
        /// <returns>Every failure, in field order; empty when the answers pass.</returns>
        public static IReadOnlyList<ValidationErrorModel> Validate(
            JsonElement answers,
            DateOnly today,
            int formSpecVersion = 0)
        {
            var errors = new List<ValidationErrorModel>();

            ValidateRequestType(answers, errors);
            ValidateIdentifier(answers, errors);
            ValidateDateOfBirth(answers, today, errors);
            ValidateContact(answers, formSpecVersion, errors);

            return errors;
        }

        /// <summary>
        /// A service type the form does not offer is a probe, not a mistake: the
        /// selectors are required by the spec and only offer known values. A
        /// missing selector is the spec's required rule to report, not this one's;
        /// a present value that resolves to no request type is refused here,
        /// because the mapper would otherwise default it to a new application.
        /// </summary>
        private static void ValidateRequestType(JsonElement answers, List<ValidationErrorModel> errors)
        {
            if (BusPassAnswers.TryGetRequestType(answers, out _))
            {
                return;
            }

            if (BusPassAnswers.GetString(answers, BusPassAnswers.ServiceRequestType) is not null)
            {
                errors.Add(RequestTypeInvalid(BusPassAnswers.ServiceRequestType));
                return;
            }

            // The v1/v2 shape: a category, and for an existing client a reason.
            string? category = BusPassAnswers.GetString(answers, BusPassAnswers.ApplicantCategory);
            if (category is null)
            {
                return;
            }

            if (category == "existing")
            {
                // No reason at all is the spec's conditional required rule.
                if (BusPassAnswers.GetString(answers, BusPassAnswers.ExistingClientReason) is not null)
                {
                    errors.Add(RequestTypeInvalid(BusPassAnswers.ExistingClientReason));
                }

                return;
            }

            errors.Add(RequestTypeInvalid(BusPassAnswers.ApplicantCategory));
        }

        private static ValidationErrorModel RequestTypeInvalid(string field) =>
            new()
            {
                Field = field,
                Keyword = BusPassErrorKeywords.RequestTypeInvalid,
                Message = "Select a valid service type",
            };

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

        private static void ValidateContact(JsonElement answers, int formSpecVersion, List<ValidationErrorModel> errors)
        {
            string? phone = answers.TryGetProperty(BusPassAnswers.PhoneNumber, out JsonElement value)
                && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            Match match = LegacyPhonePattern.Match(phone ?? string.Empty);
            // .NET's $ also matches before a trailing newline.
            if (phone is null || !match.Success || match.Length != phone.Length)
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = BusPassAnswers.PhoneNumber,
                    Keyword = ValidationKeywords.PhoneInvalidFormat,
                    Message = "Phone number is invalid",
                });
            }

            string? email = BusPassAnswers.GetString(answers, BusPassAnswers.Email);

            bool prefersEmail = BusPassAnswers.GetString(answers, BusPassAnswers.PreferredCommunication) == "email";
            if (prefersEmail && email is null)
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = BusPassAnswers.Email,
                    Keyword = BusPassErrorKeywords.EmailRequiredForEmailContact,
                    Message = "An email address is required when email is the preferred means of communication",
                });
            }

            // From v6 the spec itself names the partner field, and the spec
            // validator reports the mismatch; earlier versions checked it only in
            // browser script, so this is their server-side check.
            if (formSpecVersion < SpecValidatesEmailMatchVersion
                && email is not null
                && !EmailAddress.ConfirmationMatches(email, BusPassAnswers.GetString(answers, BusPassAnswers.EmailVerification)))
            {
                errors.Add(new ValidationErrorModel
                {
                    Field = BusPassAnswers.EmailVerification,
                    Keyword = BusPassErrorKeywords.EmailMismatch,
                    Message = "The two email addresses do not match",
                });
            }
        }
    }
}
