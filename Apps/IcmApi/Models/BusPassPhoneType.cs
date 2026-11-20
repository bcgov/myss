namespace Icm.Api.Models
{
    /// <summary>
    /// Which kind of number the applicant's phone number is. The workflow has a field per
    /// kind rather than a type value; the home field always carries the number, and Work
    /// and Cell also fill their own field.
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
