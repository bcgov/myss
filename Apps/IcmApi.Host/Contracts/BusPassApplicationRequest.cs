namespace Icm.Api.Host.Contracts
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Icm.Api.Models;

    /// <summary>
    /// A bus pass request in MySS business terms: the body of
    /// <c>POST v1/bus-pass/applications</c>. It mirrors MyssApi's
    /// <c>BusPassApplicationModel</c> field for field; the shared sample in
    /// <c>Shared/contracts/bus-pass-application.sample.json</c> is the contract
    /// both test suites check against.
    /// </summary>
    /// <remarks>
    /// The enumerations are the client library's own, so the values a caller
    /// writes ("NewApplication", "Over65", "Cell", "Email") are the ones the
    /// library maps to Siebel's vocabulary. Validation of the facts (one of SIN or
    /// account number, an email when email is preferred) is the caller's job; this
    /// host transmits what it is given.
    /// </remarks>
    public class BusPassApplicationRequest
    {
        /// <summary>
        /// Gets or sets the caller's key for this submission, so that a resend after
        /// an ambiguous failure carries the same key and ICM's upsert can recognise
        /// it. MyssApi sends its stored submission id. Letters, digits and hyphens
        /// only, at most 64 of them, enforced: anything else is refused with a 400
        /// before the library sees it.
        /// </summary>
        [RegularExpression(SubmissionKeyPattern, ErrorMessage = "submissionKey may contain only letters, digits and hyphens, up to 64 characters.")]
        public string? SubmissionKey { get; set; }

        /// <summary>The shape a submission key must have.</summary>
        public const string SubmissionKeyPattern = "^[A-Za-z0-9-]{1,64}$";

        /// <summary>Gets or sets what the applicant is asking for.</summary>
        public required BusPassRequestType RequestType { get; set; }

        /// <summary>Gets or sets the eligibility category a new applicant claims.</summary>
        public BusPassApplicantType? ApplicantType { get; set; }

        /// <summary>Gets or sets whether a replacement requester acknowledged the old pass will be cancelled.</summary>
        public bool? AcknowledgedPassCancellation { get; set; }

        /// <summary>Gets or sets whether a new applicant acknowledged the eligibility criteria.</summary>
        public bool? AcknowledgedEligibilityCriteria { get; set; }

        /// <summary>Gets or sets the social insurance number, digits only.</summary>
        public string? SocialInsuranceNumber { get; set; }

        /// <summary>Gets or sets the existing bus pass account number, digits only.</summary>
        public string? BusPassAccountNumber { get; set; }

        /// <summary>Gets or sets the applicant's first name.</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets the applicant's last name.</summary>
        public string? LastName { get; set; }

        /// <summary>Gets or sets the applicant's date of birth.</summary>
        public DateOnly? DateOfBirth { get; set; }

        /// <summary>Gets or sets the phone number, digits only.</summary>
        public string? PhoneNumber { get; set; }

        /// <summary>Gets or sets which kind of number <see cref="PhoneNumber"/> is.</summary>
        public BusPassPhoneType? PhoneType { get; set; }

        /// <summary>Gets or sets whether a message may be left at that number.</summary>
        public bool? LeaveMessageAllowed { get; set; }

        /// <summary>Gets or sets the email address for notifications.</summary>
        public string? EmailAddress { get; set; }

        /// <summary>Gets or sets how the applicant prefers to be contacted.</summary>
        public BusPassContactMethod? PreferredContactMethod { get; set; }

        /// <summary>Gets or sets the residential address.</summary>
        public BusPassAddressRequest? ResidentialAddress { get; set; }

        /// <summary>Gets or sets the mailing address, only when it differs from the residential one.</summary>
        public BusPassAddressRequest? MailingAddress { get; set; }
    }
}
