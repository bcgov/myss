namespace Icm.Api.Models
{
    /// <summary>
    /// The eligibility category a new applicant claims.
    /// </summary>
    public enum BusPassApplicantType
    {
        /// <summary>The applicant is 65 or older.</summary>
        Over65,

        /// <summary>The applicant is a First Nations person receiving assistance.</summary>
        FirstNations,

        /// <summary>Neither of the above.</summary>
        Neither,
    }
}
