namespace Myss.Api.Providers
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stands in for the ICM contact match until it is connected. For the MVP
    /// every registrant is taken to be known to ICM already, so every match
    /// succeeds and no ICM contact is created; creating one is separate work.
    /// </summary>
    public class HardcodedContactMatchProvider : IContactMatchProvider
    {
        private static readonly ContactMatchResult Matched = new(IsMatch: true);

        /// <inheritdoc/>
        public Task<ContactMatchResult> MatchAsync(ContactMatchCriteria criteria, CancellationToken cancellationToken)
        {
            return Task.FromResult(Matched);
        }
    }
}
