namespace Myss.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// What the applicant is asking the BC Bus Pass Program for.
    /// </summary>
    public enum BusPassRequestType
    {
        /// <summary>A new applicant requesting a pass.</summary>
        NewApplication,

        /// <summary>An existing client who has moved.</summary>
        AddressUpdate,

        /// <summary>An existing client whose pass was lost or stolen.</summary>
        Replacement,
    }

    /// <summary>
    /// The eligibility category a new applicant claims.
    /// </summary>
    public enum BusPassApplicantType
    {
        /// <summary>Over 65 and within the ten-year residency requirement.</summary>
        Over65,

        /// <summary>Living on a First Nations reserve and receiving band office assistance.</summary>
        FirstNations,

        /// <summary>Neither of the above.</summary>
        Neither,
    }

    /// <summary>
    /// Which kind of number the applicant's phone number is.
    /// </summary>
    public enum BusPassPhoneType
    {
        /// <summary>A home number.</summary>
        Home,

        /// <summary>A work number.</summary>
        Work,

        /// <summary>A mobile number.</summary>
        Cell,
    }

    /// <summary>
    /// How the applicant prefers to be contacted.
    /// </summary>
    public enum BusPassContactMethod
    {
        /// <summary>By phone.</summary>
        Phone,

        /// <summary>By email.</summary>
        Email,
    }

    /// <summary>
    /// A postal address as the applicant stated it.
    /// </summary>
    public class BusPassAddressModel
    {
        /// <summary>Gets or sets the unit or apartment number.</summary>
        public string? Unit { get; set; }

        /// <summary>Gets or sets the first street address line.</summary>
        public string? Line1 { get; set; }

        /// <summary>Gets or sets the second street address line.</summary>
        public string? Line2 { get; set; }

        /// <summary>Gets or sets the city.</summary>
        public string? City { get; set; }

        /// <summary>Gets or sets the province, normalised to "BC".</summary>
        public string? Province { get; set; }

        /// <summary>Gets or sets the postal code.</summary>
        public string? PostalCode { get; set; }
    }

    /// <summary>
    /// A bus pass request in business terms: the body this API sends to the ICM
    /// middleware's <c>POST v1/bus-pass/applications</c>. It carries the same
    /// facts the legacy <c>/BusPass</c> form captured; the middleware turns them
    /// into Siebel's shape, and nothing Siebel-flavoured appears here.
    /// </summary>
    /// <remarks>
    /// Some facts have no field on the ICM side yet (the eligibility category,
    /// the two acknowledgements and the leave-a-message consent). They are still
    /// sent so the whole request is stated once and the middleware decides what
    /// reaches ICM.
    /// </remarks>
    public class BusPassApplicationModel
    {
        /// <summary>Gets or sets what the applicant is asking for.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter<BusPassRequestType>))]
        public required BusPassRequestType RequestType { get; set; }

        /// <summary>Gets or sets the eligibility category a new applicant claims.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter<BusPassApplicantType>))]
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
        [JsonConverter(typeof(JsonStringEnumConverter<BusPassPhoneType>))]
        public BusPassPhoneType? PhoneType { get; set; }

        /// <summary>Gets or sets whether a message may be left at that number.</summary>
        public bool? LeaveMessageAllowed { get; set; }

        /// <summary>Gets or sets the email address for notifications.</summary>
        public string? EmailAddress { get; set; }

        /// <summary>Gets or sets how the applicant prefers to be contacted.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter<BusPassContactMethod>))]
        public BusPassContactMethod? PreferredContactMethod { get; set; }

        /// <summary>Gets or sets the residential address.</summary>
        public BusPassAddressModel? ResidentialAddress { get; set; }

        /// <summary>Gets or sets the mailing address, only when it differs from the residential one.</summary>
        public BusPassAddressModel? MailingAddress { get; set; }
    }

    /// <summary>
    /// The middleware's answer to a bus pass submission. A business rejection is
    /// an ordinary outcome carrying an error code, not an exception, because ICM
    /// still files a service request for it and still returns that number.
    /// </summary>
    public class BusPassSubmissionOutcomeModel
    {
        /// <summary>Gets or sets the reference number ICM assigned, if any.</summary>
        public string? ApplicationNumber { get; set; }

        /// <summary>Gets or sets ICM's error code when the request was not accepted.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Gets or sets ICM's error message when the request was not accepted.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Gets or sets the status word the workflow reported.</summary>
        public string? Status { get; set; }

        /// <summary>
        /// Gets a value indicating whether ICM accepted the request. An
        /// application number alone is not proof: a rejected match still gets one.
        /// </summary>
        [JsonIgnore]
        public bool IsAccepted => string.IsNullOrWhiteSpace(ErrorCode);
    }

    /// <summary>
    /// How the hand-off to ICM ended, as reported to the citizen.
    /// </summary>
    public enum BusPassSubmissionOutcome
    {
        /// <summary>ICM accepted the request and assigned a reference number.</summary>
        Accepted,

        /// <summary>ICM received the request and declined it.</summary>
        Rejected,

        /// <summary>The submission is stored but could not be delivered to ICM.</summary>
        Failed,
    }

    /// <summary>
    /// What the citizen gets back after a bus pass submission: where it was
    /// stored, and what the ministry's case system said.
    /// </summary>
    public class BusPassSubmissionResponseModel
    {
        /// <summary>Gets or sets the stored submission identifier.</summary>
        public required Guid SubmissionId { get; set; }

        /// <summary>Gets or sets the logical form identifier.</summary>
        public required string FormSpecId { get; set; }

        /// <summary>Gets or sets the spec version the form was rendered with.</summary>
        public required int FormSpecVersion { get; set; }

        /// <summary>Gets or sets the reference number to quote, when ICM assigned one.</summary>
        public string? ReferenceNumber { get; set; }

        /// <summary>Gets or sets how the dispatch to ICM ended.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter<BusPassSubmissionOutcome>))]
        public required BusPassSubmissionOutcome Outcome { get; set; }

        /// <summary>Gets or sets the stable keyword for a non-accepted outcome, for the client to match on.</summary>
        public string? Keyword { get; set; }

        /// <summary>Gets or sets ICM's error code for a rejected request.</summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// The result of a bus pass submission attempt: the response to return, or
    /// the validation failures that stopped anything being stored.
    /// </summary>
    public class BusPassSubmissionResultModel
    {
        /// <summary>Gets or sets the response. Null when validation failed.</summary>
        public BusPassSubmissionResponseModel? Response { get; set; }

        /// <summary>Gets or sets every reason the submission was refused.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; set; } = [];

        /// <summary>Gets a value indicating whether the submission passed validation and was stored.</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>Creates a result for a stored submission, whatever ICM said about it.</summary>
        /// <param name="response">The response to return.</param>
        /// <returns>A valid result.</returns>
        public static BusPassSubmissionResultModel Completed(BusPassSubmissionResponseModel response) =>
            new() { Response = response };

        /// <summary>Creates a refused result.</summary>
        /// <param name="errors">Every reason for refusal.</param>
        /// <returns>An invalid result.</returns>
        public static BusPassSubmissionResultModel Refused(IReadOnlyList<ValidationErrorModel> errors) =>
            new() { Errors = errors };
    }

    /// <summary>
    /// Stable dotted keywords for the bus pass module. The keyword is the
    /// contract: the frontend matches on it and it doubles as the content key
    /// for the user-facing text (handbook §2.6).
    /// </summary>
    public static class BusPassErrorKeywords
    {
        /// <summary>ICM received the request and declined it.</summary>
        public const string Rejected = "BUSPASS.SUBMIT.REJECTED";

        /// <summary>The request is stored but could not be delivered to ICM.</summary>
        public const string IcmUnavailable = "BUSPASS.SUBMIT.ICM_UNAVAILABLE";

        /// <summary>Neither a SIN nor a bus pass account number was given.</summary>
        public const string IdentifierRequired = "BUSPASS.IDENTITY.IDENTIFIER_REQUIRED";

        /// <summary>The three date-of-birth parts do not make a real date.</summary>
        public const string DateOfBirthInvalid = "BUSPASS.DOB.INVALID";

        /// <summary>The date of birth is after today.</summary>
        public const string DateOfBirthInFuture = "BUSPASS.DOB.FUTURE";

        /// <summary>The applicant is under sixteen.</summary>
        public const string Under16 = "BUSPASS.DOB.UNDER_16";

        /// <summary>Email was chosen as the contact method but no address was given.</summary>
        public const string EmailRequiredForEmailContact = "BUSPASS.CONTACT.EMAIL_REQUIRED";
    }
}
