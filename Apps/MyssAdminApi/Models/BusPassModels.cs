namespace Myss.AdminApi.Models
{
    /// <summary>
    /// What the applicant is asking the BC Bus Pass Program for. A copy of
    /// MyssApi's enum: the bus pass PDF reads it from the stored answers through
    /// <see cref="Services.BusPassAnswers"/>, which MyssApi's submission rules
    /// also use, so the two copies must read the answers the same way.
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
}
