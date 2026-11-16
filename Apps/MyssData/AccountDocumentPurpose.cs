namespace Myss.Data
{
    /// <summary>
    /// What a document linked to an account at registration is. Stored by name
    /// on <see cref="AccountDocument"/>.
    /// </summary>
    public enum AccountDocumentPurpose
    {
        /// <summary>A signed Consent to Disclosure of Information form (HR3189) for the citizen's helper.</summary>
        ConsentToDisclosure,

        /// <summary>Power of attorney documents from a legal representative.</summary>
        PowerOfAttorney,
    }
}
