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
    /// <b>Two of these fields are not transmitted.</b> The workflow's integration
    /// object (<c>ICMSRBusPassInboundIO</c>) has no field for
    /// <see cref="AcknowledgedPassCancellation"/> or
    /// <see cref="AcknowledgedEligibilityCriteria"/>, and MEASURED SIT2 2026-09-14 the
    /// old form's own submissions leave no trace of them either. They are kept here so
    /// the caller states the whole request once. <see cref="ApplicantType"/> travels as
    /// the SR sub type (First Nations) or not at all (Over 65 / Neither file identically
    /// on the old path); <see cref="LeaveMessageAllowed"/> travels as the old path does,
    /// by repeating the number in <c>Alternate Phone #</c>.
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
        /// <summary>
        /// Gets or sets a key that identifies this submission to ICM, so that resending
        /// the same application — after a timeout, say, when the first attempt may have
        /// been accepted and its answer lost — carries the same key rather than a fresh
        /// one and the workflow's upsert can recognise it. Null makes the client generate
        /// a key unique to the call, which is right for a single attempt and wrong for a
        /// retry. Letters, digits and hyphens; a GUID is a good choice. The client
        /// prefixes it with <c>MYSS-</c> on the wire.
        /// </summary>
        /// <remarks>
        /// That a repeated key makes the workflow update the earlier service request
        /// rather than file a second one is what Siebel's upsert semantics say and what
        /// the SBL-EAI-04397 failure without a key implies; it has not been exercised
        /// against a live ICM.
        /// </remarks>
        public string? SubmissionKey { get; set; }

        /// <summary>Gets or sets what the applicant is asking for.</summary>
        public required BusPassRequestType RequestType { get; set; }

        /// <summary>
        /// Gets or sets the eligibility category a new applicant claims. First Nations
        /// files as the <c>AANDC Online Request</c> sub type; Over 65 and Neither file as
        /// a plain <c>Application</c>, as they do from the old form.
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
        /// Gets or sets whether a message may be left at that number. Sent the way the old
        /// path sends it: the number is repeated in <c>Alternate Phone #</c> when true.
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
        /// Makes the submission a <c>Multiple Addresses</c> one and is sent as a second
        /// applicant row with purpose <c>Mailing</c>. MEASURED SIT2 2026-09-14: the
        /// workflow stores one applicant row either way (the old path's does too), so
        /// what it does with the second row is not visible from the service request.
        /// </summary>
        public BusPassAddress? MailingAddress { get; set; }

        /// <summary>Gets or sets files to attach to the submission.</summary>
        public IReadOnlyList<BusPassAttachment>? Attachments { get; set; }
    }
}
