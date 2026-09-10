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
    /// then dispatch to ICM through the middleware and record what happened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is deliberate and mirrors the attachments pipeline and the
    /// handbook's audit-before-promotion rule: the submission and a started
    /// event are written before the call to ICM, so a crash mid-call leaves a
    /// findable record rather than a request ICM may have and MySS has no trace
    /// of. Because ICM files a service request on every call and has no
    /// idempotency key, a started event with no closing event is the signal
    /// that a submission must never be re-sent automatically.
    /// </para>
    /// <para>
    /// The log is append-only. A rejection from ICM and a failure to reach it
    /// are both recorded as events and both returned as outcomes; the citizen's
    /// submission exists in MySS in every case, and only the reference number
    /// depends on ICM.
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
        private readonly ICorrelationIdAccessor _correlationIdAccessor;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassSubmissionService"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="formsService">Injected forms service, for validation and storage.</param>
        /// <param name="submissionProvider">Injected boundary to the ICM middleware.</param>
        /// <param name="correlationIdAccessor">Injected correlation id accessor, to stamp the log.</param>
        /// <param name="timeProvider">Injected clock.</param>
        public BusPassSubmissionService(
            ILogger<BusPassSubmissionService> logger,
            FormsDbContext dbContext,
            IFormsService formsService,
            IBusPassSubmissionProvider submissionProvider,
            ICorrelationIdAccessor correlationIdAccessor,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _formsService = formsService;
            _submissionProvider = submissionProvider;
            _correlationIdAccessor = correlationIdAccessor;
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

            Guid attemptId = Guid.NewGuid();
            string? requestId = _correlationIdAccessor.CorrelationId;
            await AppendAsync(
                submission.Id,
                attemptId,
                requestId,
                BusPassDispatchEventType.Started,
                cancellationToken: cancellationToken);

            BusPassSubmissionOutcomeModel outcome;
            try
            {
                outcome = await _submissionProvider.SubmitAsync(application, cancellationToken);
            }
            catch (IcmApiUnavailableException ex)
            {
                // Recorded even when the caller has gone away: the row is the
                // evidence an operator needs to decide whether ICM has this.
                await AppendAsync(
                    submission.Id,
                    attemptId,
                    requestId,
                    BusPassDispatchEventType.Failed,
                    errorMessage: ex.Message,
                    cancellationToken: CancellationToken.None);

                _logger.LogError(
                    ex,
                    "Bus pass submission {SubmissionId} could not be delivered to the ICM middleware (attempt {AttemptId})",
                    submission.Id,
                    attemptId);

                return BusPassSubmissionResultModel.Completed(
                    ToResponse(submission, BusPassSubmissionOutcome.Failed, null, null, BusPassErrorKeywords.IcmUnavailable));
            }

            BusPassDispatchEventType closing = outcome.IsAccepted
                ? BusPassDispatchEventType.Accepted
                : BusPassDispatchEventType.Rejected;
            await AppendAsync(
                submission.Id,
                attemptId,
                requestId,
                closing,
                outcome.ApplicationNumber,
                outcome.ErrorCode,
                outcome.ErrorMessage,
                cancellationToken);

            // Ids and codes only; the answers and ICM's free text stay out of the log.
            _logger.LogInformation(
                "Bus pass submission {SubmissionId} dispatched: {Outcome}, reference {ReferenceNumber}, error code {ErrorCode}",
                submission.Id,
                closing,
                outcome.ApplicationNumber ?? "-",
                outcome.ErrorCode ?? "-");

            return outcome.IsAccepted
                ? BusPassSubmissionResultModel.Completed(
                    ToResponse(submission, BusPassSubmissionOutcome.Accepted, outcome.ApplicationNumber, null, null))
                : BusPassSubmissionResultModel.Completed(
                    ToResponse(submission, BusPassSubmissionOutcome.Rejected, outcome.ApplicationNumber, outcome.ErrorCode, BusPassErrorKeywords.Rejected));
        }

        private async Task AppendAsync(
            Guid submissionId,
            Guid attemptId,
            string? requestId,
            BusPassDispatchEventType type,
            string? referenceNumber = null,
            string? errorCode = null,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            _dbContext.BusPassDispatchEvents.Add(new BusPassDispatchEvent
            {
                Id = Guid.NewGuid(),
                SubmissionId = submissionId,
                AttemptId = attemptId,
                Type = type,
                OccurredAt = _timeProvider.GetUtcNow(),
                RequestId = requestId,
                ReferenceNumber = referenceNumber,
                ErrorCode = errorCode,
                ErrorMessage = Truncate(errorMessage),
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static BusPassSubmissionResponseModel ToResponse(
            FormSubmissionResponseModel submission,
            BusPassSubmissionOutcome outcome,
            string? referenceNumber,
            string? errorCode,
            string? keyword)
        {
            return new BusPassSubmissionResponseModel
            {
                SubmissionId = submission.Id,
                FormSpecId = submission.FormSpecId,
                FormSpecVersion = submission.FormSpecVersion,
                ReferenceNumber = referenceNumber,
                Outcome = outcome,
                Keyword = keyword,
                ErrorCode = errorCode,
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
