namespace Myss.Data
{
    /// <summary>
    /// Who a registration makes the account for, as the citizen answered before
    /// signing in. Stored by name on <see cref="MyssUserProfile"/>.
    /// </summary>
    public enum AccountCreationContext
    {
        /// <summary>"I'm making an account for myself (and family, if applicable)".</summary>
        Self,

        /// <summary>"I'm making an account for myself with help from someone else".</summary>
        WithHelper,

        /// <summary>"I'm a legal representative who is creating an account on behalf of someone else".</summary>
        LegalRepresentative,

        /// <summary>"I'm a parent making an account for a minor child".</summary>
        ParentForMinor,
    }
}
