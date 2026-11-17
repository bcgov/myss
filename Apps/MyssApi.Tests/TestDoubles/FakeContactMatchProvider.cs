namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Api.Providers;

    /// <summary>
    /// Records every contact match and answers with <see cref="Result"/>.
    /// </summary>
    public sealed class FakeContactMatchProvider : IContactMatchProvider
    {
        /// <summary>Gets the criteria matched so far, oldest first.</summary>
        public List<ContactMatchCriteria> Calls { get; } = [];

        /// <summary>Gets or sets the answer to every match.</summary>
        public ContactMatchResult Result { get; set; } = new(IsMatch: true);

        /// <inheritdoc/>
        public Task<ContactMatchResult> MatchAsync(ContactMatchCriteria criteria, CancellationToken cancellationToken)
        {
            Calls.Add(criteria);
            return Task.FromResult(Result);
        }
    }
}
