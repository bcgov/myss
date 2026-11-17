namespace Myss.Data
{
    /// <summary>
    /// The kinds of phone number ICM keeps for a contact. Stored by name on
    /// <see cref="MyssUserPhone"/>, and returned as is by MyssApi's Account Info.
    /// </summary>
    public enum AccountPhoneType
    {
        /// <summary>A home number.</summary>
        Home,

        /// <summary>A mobile number.</summary>
        Cell,

        /// <summary>A work number.</summary>
        Work,

        /// <summary>A number where a message can be left.</summary>
        Message,
    }
}
