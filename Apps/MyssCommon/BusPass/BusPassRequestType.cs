namespace Myss.Common.BusPass
{
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
}
