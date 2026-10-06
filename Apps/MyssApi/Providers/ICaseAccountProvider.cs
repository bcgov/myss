namespace Myss.Api.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The case side of Account Info: what ICM (case number) and MIS
    /// PORTALSERVICES (family members, mailing address) know about the citizen.
    /// </summary>
    /// <remarks>
    /// Neither is connected yet, so <see cref="PlaceholderCaseAccountProvider"/>
    /// stands in. Connecting them is a new implementation of this interface;
    /// the service and the page do not change. MIS reads are cached once per
    /// session (ADR-0006).
    /// </remarks>
    public interface ICaseAccountProvider
    {
        /// <summary>
        /// Gets the case details for a citizen.
        /// </summary>
        /// <param name="subject">The citizen's authenticated identity subject.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The case details; empty values for a citizen with no case.</returns>
        Task<CaseAccountDetails> GetAsync(string subject, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A citizen's case details as Account Info shows them.
    /// </summary>
    /// <param name="CaseNumber">The ICM case number, or null with no case.</param>
    /// <param name="FamilyMembers">The other people on the case, by name.</param>
    /// <param name="MailingAddressLines">The mailing address, one line per entry.</param>
    public sealed record CaseAccountDetails(
        string? CaseNumber,
        IReadOnlyList<string> FamilyMembers,
        IReadOnlyList<string> MailingAddressLines);
}
