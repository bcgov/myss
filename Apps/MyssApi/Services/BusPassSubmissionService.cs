namespace Myss.Api.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Data;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// BC Bus Pass submissions: validate and store through the forms module,
    /// then dispatch to ICM through the middleware and record the outcome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is deliberate and mirrors the attachments pipeline: the
    /// submission and a pending dispatch row are written before the call to
    /// ICM, so a crash mid-call leaves a findable pending row rather than a
    /// request ICM may have and MySS has no record of. Because ICM files a
    /// service request on every call and has no idempotency key, a pending row
    /// is the signal that a submission must never be re-sent automatically.
    /// </para>
    /// <para>
    /// A rejection from ICM and a failure to reach it are both stored and both
    /// returned as outcomes. The citizen's submission exists in MySS in every
    /// case; only the reference number depends on ICM.
    /// </para>
    /// </remarks>
    public class BusPassSubmissionService : IBusPassSubmissionService
    {
        /// <summary>The form spec this service accepts submissions for.</summary>
        public const string FormSpecId = BusPassPdfFieldMap.FormSpecId;

        private const int ErrorMessageMaxLength = 1024;

        private readonly ILogger<BusPassSubmissionService> _logger;
        private readonly FormsDbContext _dbContext;
        private readonly IFormsService _formsService;
        private readonly IBusPassSubmissionProvider _submissionProvider;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassSubmissionService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="formsService">Injected forms service, for validation and storage.</param>
        /// <param name="submissionProvider">Injected boundary to the ICM middleware.</param>
        /// <param name="timeProvider">Injected clock.</param>
        public BusPassSubmissionService(
            ILogger<BusPassSubmissionService> logger,
            FormsDbContext dbContext,
            IFormsService formsService,
            IBusPassSubmissionProvider submissionProvider,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _formsService = formsService;
            _submissionProvider = submissionProvider;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public async Task<BusPassSubmissionResultModel> SubmitAsync(
            FormSubmissionRequestModel request,
            CancellationToken cancellationToken)
        {
            DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

            // Spec validation and the bus pass rules run together, so the
            // citizen sees every failure in one response and nothing is stored
            // until all of them pass.
            FormSubmissionResultModel stored = await _formsService.SubmitAsync(
                FormSpecId,
                request,
                answers => BusPassRules.Validate(answers, today),
                cancellationToken);

            if (!stored.IsValid)
            {
                return BusPassSubmissionResultModel.Refused(stored.Errors);
            }

            FormSubmissionResponseModel submission = stored.Submission!;
            BusPassApplicationModel application = BusPassApplicationMapper.Build(submission.Answers);

            var dispatch = new BusPassDispatch
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                AttemptedAt = _timeProvider.GetUtcNow(),
                Outcome = BusPassDispatchOutcome.Pending,
            };
            _dbContext.BusPassDispatches.Add(dispatch);
            await _dbContext.SaveChangesAsync(cancellationToken);

            BusPassSubmissionOutcomeModel outcome;
            try
            {
                outcome = await _submissionProvider.SubmitAsync(application, cancellationToken);
            }
            catch (IcmApiUnavailableException ex)
            {
                dispatch.Outcome = BusPassDispatchOutcome.Failed;
                dispatch.CompletedAt = _timeProvider.GetUtcNow();
                dispatch.ErrorMessage = Truncate(ex.Message);

                // Recorded even when the caller has gone away: the row is the
                // evidence an operator needs to decide whether ICM has this.
                await _dbContext.SaveChangesAsync(CancellationToken.None);

                _logger.LogError(
                    ex,
                    "Bus pass submission {SubmissionId} could not be delivered to the ICM middleware",
                    submission.Id);

                return BusPassSubmissionResultModel.Completed(ToResponse(submission, dispatch, BusPassErrorKeywords.IcmUnavailable));
            }

            dispatch.Outcome = outcome.IsAccepted ? BusPassDispatchOutcome.Accepted : BusPassDispatchOutcome.Rejected;
            dispatch.CompletedAt = _timeProvider.GetUtcNow();
            dispatch.ReferenceNumber = outcome.ApplicationNumber;
            dispatch.ErrorCode = outcome.ErrorCode;
            dispatch.ErrorMessage = Truncate(outcome.ErrorMessage);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Ids and codes only; the answers and ICM's free text stay out of the log.
            _logger.LogInformation(
                "Bus pass submission {SubmissionId} dispatched: {Outcome}, reference {ReferenceNumber}, error code {ErrorCode}",
                submission.Id,
                dispatch.Outcome,
                dispatch.ReferenceNumber ?? "-",
                dispatch.ErrorCode ?? "-");

            string? keyword = outcome.IsAccepted ? null : BusPassErrorKeywords.Rejected;
            return BusPassSubmissionResultModel.Completed(ToResponse(submission, dispatch, keyword));
        }

        private static BusPassSubmissionResponseModel ToResponse(
            FormSubmissionResponseModel submission,
            BusPassDispatch dispatch,
            string? keyword)
        {
            return new BusPassSubmissionResponseModel
            {
                SubmissionId = submission.Id,
                FormSpecId = submission.FormSpecId,
                FormSpecVersion = submission.FormSpecVersion,
                ReferenceNumber = dispatch.ReferenceNumber,
                Outcome = dispatch.Outcome,
                Keyword = keyword,
                ErrorCode = dispatch.ErrorCode,
            };
        }

        private static string? Truncate(string? text)
        {
            if (text is null || text.Length <= ErrorMessageMaxLength)
            {
                return text;
            }

            return text[..ErrorMessageMaxLength];
        }
    }
}
