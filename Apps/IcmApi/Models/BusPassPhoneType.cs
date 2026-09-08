namespace Icm.Api.Models
{
    /// <summary>
    /// Which kind of number the applicant's phone number is. The workflow has a separate
    /// field per kind, so this decides where the number goes rather than travelling as a
    /// value of its own.
    /// </summary>
    public enum BusPassPhoneType
    {
        /// <summary>A home number.</summary>
        Home,

        /// <summary>A work number.</summary>
        Work,

        /// <summary>A cell number.</summary>
        Cell,
    }
}
