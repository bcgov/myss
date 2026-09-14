namespace Myss.Api.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;

    /// <summary>
    /// The BC Bus Pass submission flow: validate, store, hand to ICM through the
    /// middleware, record what came back.
    /// </summary>
    public interface IBusPassSubmissionService
    {
        /// <summary>
        /// Submits a bus pass request.
        /// </summary>
        /// <param name="request">The submission payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The stored submission with ICM's answer, or the validation failures
        /// that stopped it being stored. A failed hand-off to ICM is reported in
        /// the response's outcome, not thrown: the submission is kept either way.
        /// </returns>
        Task<BusPassSubmissionResultModel> SubmitAsync(
            FormSubmissionRequestModel request,
            CancellationToken cancellationToken);
    }
}
