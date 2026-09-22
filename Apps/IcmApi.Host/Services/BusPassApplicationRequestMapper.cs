namespace Icm.Api.Host.Services
{
    using System;
    using Icm.Api.Host.Contracts;
    using Icm.Api.Models;

    /// <summary>
    /// Turns the wire request into the client library's model. Pure and static so
    /// the mapping is testable on its own.
    /// </summary>
    public static class BusPassApplicationRequestMapper
    {
        /// <summary>
        /// Maps the request.
        /// </summary>
        /// <param name="request">The request as received.</param>
        /// <returns>The application to submit.</returns>
        public static BusPassApplication ToApplication(BusPassApplicationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new BusPassApplication
            {
                SubmissionKey = string.IsNullOrWhiteSpace(request.SubmissionKey) ? null : request.SubmissionKey.Trim(),
                RequestType = request.RequestType,
                ApplicantType = request.ApplicantType,
                AcknowledgedPassCancellation = request.AcknowledgedPassCancellation,
                AcknowledgedEligibilityCriteria = request.AcknowledgedEligibilityCriteria,
                SocialInsuranceNumber = request.SocialInsuranceNumber,
                BusPassAccountNumber = request.BusPassAccountNumber,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                PhoneNumber = request.PhoneNumber,
                PhoneType = request.PhoneType,
                LeaveMessageAllowed = request.LeaveMessageAllowed,
                EmailAddress = request.EmailAddress,
                PreferredContactMethod = request.PreferredContactMethod,
                ResidentialAddress = ToAddress(request.ResidentialAddress),
                MailingAddress = ToAddress(request.MailingAddress),
            };
        }

        private static BusPassAddress? ToAddress(BusPassAddressRequest? address)
        {
            if (address is null)
            {
                return null;
            }

            return new BusPassAddress
            {
                Unit = address.Unit,
                Line1 = address.Line1,
                Line2 = address.Line2,
                City = address.City,
                Province = address.Province,
                PostalCode = address.PostalCode,
            };
        }
    }
}
