namespace Myss.Api.Services
{
    using System;
    using System.Text.Json;
    using Myss.Api.Models;

    /// <summary>
    /// Turns a stored BC Bus Pass submission's answers into the business-language
    /// request the ICM middleware accepts. Pure and static, like
    /// <see cref="BusPassPdfDataBuilder"/>, so the mapping is testable on its own.
    /// </summary>
    /// <remarks>
    /// Runs on answers the rules have already accepted (<see cref="BusPassRules"/>),
    /// so a missing conditional value here is mapped to null rather than refused.
    /// </remarks>
    public static class BusPassApplicationMapper
    {
        /// <summary>
        /// Builds the middleware request from the answers.
        /// </summary>
        /// <param name="answers">The submitted answers, keyed by component key.</param>
        /// <returns>The request to send.</returns>
        public static BusPassApplicationModel Build(JsonElement answers)
        {
            bool isNewApplicant = BusPassAnswers.GetString(answers, BusPassAnswers.ApplicantCategory) == "new";
            bool mailingDiffers = BusPassAnswers.GetString(answers, BusPassAnswers.MailingAddressDifferent) == "yes";

            return new BusPassApplicationModel
            {
                RequestType = ToRequestType(answers),
                ApplicantType = isNewApplicant ? ToApplicantType(answers) : null,
                AcknowledgedEligibilityCriteria = isNewApplicant
                    ? BusPassAnswers.GetBool(answers, BusPassAnswers.EligibilityAcknowledged)
                    : null,

                // The current spec has no replacement-fee acknowledgement, so
                // there is nothing to carry; the property stays for when it does.
                AcknowledgedPassCancellation = null,

                SocialInsuranceNumber = BusPassAnswers.Digits(BusPassAnswers.GetString(answers, BusPassAnswers.SocialInsuranceNumber)),
                BusPassAccountNumber = BusPassAnswers.Digits(BusPassAnswers.GetString(answers, BusPassAnswers.BusPassAccountNumber)),
                FirstName = BusPassAnswers.GetString(answers, BusPassAnswers.FirstName),
                LastName = BusPassAnswers.GetString(answers, BusPassAnswers.LastName),
                DateOfBirth = BusPassAnswers.TryGetDateOfBirth(answers, out DateOnly dateOfBirth) ? dateOfBirth : null,

                PhoneNumber = BusPassAnswers.Digits(BusPassAnswers.GetString(answers, BusPassAnswers.PhoneNumber)),
                PhoneType = ToPhoneType(answers),
                LeaveMessageAllowed = BusPassAnswers.GetBool(answers, BusPassAnswers.LeaveMessage),
                EmailAddress = BusPassAnswers.GetString(answers, BusPassAnswers.Email),
                PreferredContactMethod = ToContactMethod(answers),

                ResidentialAddress = new BusPassAddressModel
                {
                    Line1 = BusPassAnswers.GetString(answers, BusPassAnswers.StreetAddress1),
                    Line2 = BusPassAnswers.GetString(answers, BusPassAnswers.StreetAddress2),
                    City = BusPassAnswers.GetString(answers, BusPassAnswers.City),
                    Province = BusPassAnswers.NormalizeProvince(BusPassAnswers.GetString(answers, BusPassAnswers.Province)),
                    PostalCode = BusPassAnswers.GetString(answers, BusPassAnswers.PostalCode),
                },
                MailingAddress = mailingDiffers
                    ? new BusPassAddressModel
                    {
                        Line1 = BusPassAnswers.GetString(answers, BusPassAnswers.MailingStreetAddress1),
                        Line2 = BusPassAnswers.GetString(answers, BusPassAnswers.MailingStreetAddress2),
                        City = BusPassAnswers.GetString(answers, BusPassAnswers.MailingCity),
                        Province = BusPassAnswers.NormalizeProvince(BusPassAnswers.GetString(answers, BusPassAnswers.MailingProvince)),
                        PostalCode = BusPassAnswers.GetString(answers, BusPassAnswers.MailingPostalCode),
                    }
                    : null,
            };
        }

        private static BusPassRequestType ToRequestType(JsonElement answers)
        {
            if (BusPassAnswers.GetString(answers, BusPassAnswers.ApplicantCategory) != "existing")
            {
                return BusPassRequestType.NewApplication;
            }

            return BusPassAnswers.GetString(answers, BusPassAnswers.ExistingClientReason) == "replacement"
                ? BusPassRequestType.Replacement
                : BusPassRequestType.AddressUpdate;
        }

        private static BusPassApplicantType? ToApplicantType(JsonElement answers)
        {
            return BusPassAnswers.GetString(answers, BusPassAnswers.EligibilityCategory) switch
            {
                "over65" => BusPassApplicantType.Over65,
                "firstNations" => BusPassApplicantType.FirstNations,
                "neither" => BusPassApplicantType.Neither,
                _ => null,
            };
        }

        private static BusPassPhoneType? ToPhoneType(JsonElement answers)
        {
            return BusPassAnswers.GetString(answers, BusPassAnswers.PhoneType) switch
            {
                "home" => BusPassPhoneType.Home,
                "work" => BusPassPhoneType.Work,
                "cell" => BusPassPhoneType.Cell,
                _ => null,
            };
        }

        private static BusPassContactMethod? ToContactMethod(JsonElement answers)
        {
            return BusPassAnswers.GetString(answers, BusPassAnswers.PreferredCommunication) switch
            {
                "phone" => BusPassContactMethod.Phone,
                "email" => BusPassContactMethod.Email,
                _ => null,
            };
        }
    }
}
