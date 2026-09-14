namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Fake <see cref="IBusPassSubmissionProvider"/> that records what would
    /// have gone to the ICM middleware and answers with a scripted outcome or
    /// failure, so the service and endpoint tests never touch HTTP.
    /// </summary>
    public sealed class FakeBusPassSubmissionProvider : IBusPassSubmissionProvider
    {
        /// <summary>
        /// Gets every request handed to the provider, in order.
        /// </summary>
        public List<BusPassApplicationModel> Submitted { get; } = [];

        /// <summary>
        /// Gets or sets the outcome to answer with. Defaults to an accepted
        /// request with a reference number.
        /// </summary>
        public BusPassSubmissionOutcomeModel Outcome { get; set; } = Accepted("1-TEST-0001");

        /// <summary>
        /// Gets or sets the exception to throw instead of answering, when set.
        /// </summary>
        public Exception? Failure { get; set; }

        /// <summary>
        /// Builds an accepted outcome.
        /// </summary>
        /// <param name="applicationNumber">The reference number ICM assigned.</param>
        /// <returns>The outcome.</returns>
        public static BusPassSubmissionOutcomeModel Accepted(string applicationNumber) =>
            new() { ApplicationNumber = applicationNumber, Status = "Ready" };

        /// <summary>
        /// Builds a business rejection, the shape ICM uses when no contact
        /// matched: still numbered, but with an error code.
        /// </summary>
        /// <param name="applicationNumber">The number of the error record ICM filed.</param>
        /// <param name="errorCode">ICM's error code.</param>
        /// <param name="errorMessage">ICM's error message.</param>
        /// <returns>The outcome.</returns>
        public static BusPassSubmissionOutcomeModel Rejected(string applicationNumber, string errorCode, string errorMessage) =>
            new() { ApplicationNumber = applicationNumber, ErrorCode = errorCode, ErrorMessage = errorMessage, Status = "Error" };

        /// <inheritdoc/>
        public Task<BusPassSubmissionOutcomeModel> SubmitAsync(BusPassApplicationModel application, CancellationToken cancellationToken)
        {
            Submitted.Add(application);
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(Outcome);
        }
    }
}
