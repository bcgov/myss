namespace Myss.Api.Providers
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stands in for ICM and MIS PORTALSERVICES until Account Info is connected
    /// to them (MYSS-271). No citizen has a linked case yet, so every caller
    /// gets the same marker values, which are obvious on screen and cannot be
    /// mistaken for anyone's real details.
    /// </summary>
    public class PlaceholderCaseAccountProvider : ICaseAccountProvider
    {
        /// <summary>The case number shown until ICM is connected.</summary>
        public const string CaseNumber = "CASE_NUMBER_PLACEHOLDER";

        /// <summary>The family members entry shown until MIS is connected.</summary>
        public const string FamilyMembers = "FAMILY_MEMBERS_PLACEHOLDER";

        /// <summary>The mailing address shown until MIS is connected.</summary>
        public const string MailingAddress = "MAILING_ADDRESS_PLACEHOLDER";

        private static readonly CaseAccountDetails Placeholder = new(
            CaseNumber,
            [FamilyMembers],
            [MailingAddress]);

        /// <inheritdoc/>
        public Task<CaseAccountDetails> GetAsync(string subject, CancellationToken cancellationToken)
        {
            return Task.FromResult(Placeholder);
        }
    }
}
