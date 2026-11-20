namespace Myss.Data
{
    /// <summary>
    /// The kind of person who made an account for someone else. Stored by name
    /// on <see cref="AccountRepresentative"/>.
    /// </summary>
    public enum RepresentativeType
    {
        /// <summary>A legal representative acting for the person.</summary>
        LegalRepresentative,

        /// <summary>A parent making the account for a minor child.</summary>
        Parent,
    }
}
