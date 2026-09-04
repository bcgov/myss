namespace Icm.Api.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A bus pass request as the applicant stated it — the same facts the old MCP
    /// <c>/BusPass</c> form captured, in business terms rather than form-field terms.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Some of these fields are not yet transmitted.</b> The workflow's integration
    /// object (<c>ICMSRBusPassInboundIO</c>) has no field for
    /// <see cref="ApplicantType"/>, <see cref="AcknowledgedPassCancellation"/>,
    /// <see cref="AcknowledgedEligibilityCriteria"/> or <see cref="LeaveMessageAllowed"/>,
    /// where the retired SOAP payload carried all four. They are kept here so the caller
    /// states the whole request once and the mapping lives in one place — see the README's
    /// open questions before assuming any of them reach ICM.
    /// </para>
    /// <para>
    /// This client transmits values; it does not validate them. The old form's rules —
    /// one of SIN or account number, BC-only addresses, email required when email is the
    /// preferred contact — belong to the caller, alongside the rest of its form
    /// validation.
    /// </para>
    /// </remarks>
    public class BusPassApplication
    {
        /// <summary>Gets or sets what the applicant is asking for.</summary>
        public required BusPassRequestType RequestType { get; set; }

        /// <summary>
        /// Gets or sets the eligibility category a new applicant claims.
        /// <b>Not yet transmitted</b> — the integration object has no field for it.
        /// </summary>
        public BusPassApplicantType? ApplicantType { get; set; }

        /// <summary>
        /// Gets or sets whether a replacement requester acknowledged the old pass will be
        /// cancelled. <b>Not yet transmitted.</b>
        /// </summary>
        public bool? AcknowledgedPassCancellation { get; set; }

        /// <summary>
        /// Gets or sets whether a new applicant acknowledged the eligibility criteria.
        /// <b>Not yet transmitted.</b>
        /// </summary>
        public bool? AcknowledgedEligibilityCriteria { get; set; }

        /// <summary>Gets or sets the social insurance number. Digits only on the wire.</summary>
        public string? SocialInsuranceNumber { get; set; }

        /// <summary>
        /// Gets or sets the existing bus pass account number — the other way the old form
        /// identified a client when no SIN was given.
        /// </summary>
        public string? BusPassAccountNumber { get; set; }

        /// <summary>Gets or sets the applicant's first name.</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets the applicant's last name.</summary>
        public string? LastName { get; set; }

        /// <summary>Gets or sets the applicant's date of birth.</summary>
        public DateOnly? DateOfBirth { get; set; }

        /// <summary>Gets or sets the phone number. Digits only on the wire.</summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Gets or sets which kind of number <see cref="PhoneNumber"/> is. Decides which
        /// of the workflow's typed phone fields carries it; null uses the untyped one.
        /// </summary>
        public BusPassPhoneType? PhoneType { get; set; }

        /// <summary>
        /// Gets or sets whether a message may be left at that number.
        /// <b>Not yet transmitted.</b>
        /// </summary>
        public bool? LeaveMessageAllowed { get; set; }

        /// <summary>Gets or sets the email address for notifications.</summary>
        public string? EmailAddress { get; set; }

        /// <summary>Gets or sets how the applicant prefers to be contacted.</summary>
        public BusPassContactMethod? PreferredContactMethod { get; set; }

        /// <summary>Gets or sets the residential address.</summary>
        public BusPassAddress? ResidentialAddress { get; set; }

        /// <summary>
        /// Gets or sets the mailing address, when it differs from the residential one.
        /// Sent as a second applicant row with the <c>Mailing</c> role — MEASURED SIT2
        /// 2026-09-03 that the workflow distinguishes <c>One Address</c> from
        /// <c>Multiple Addresses</c> submissions; the two-row shape itself is the
        /// mapper's inference.
        /// </summary>
        public BusPassAddress? MailingAddress { get; set; }

        /// <summary>Gets or sets files to attach to the submission.</summary>
        public IReadOnlyList<BusPassAttachment>? Attachments { get; set; }
    }
}
