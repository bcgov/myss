namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// Tests for <see cref="BusPassSubmissionService"/>: what is stored, what
    /// reaches the middleware, and what the citizen gets back for each outcome.
    /// </summary>
    public class BusPassSubmissionServiceTests
    {
        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "radio", "key": "applicantCategory", "input": true, "validate": { "required": true } },
            { "type": "radio", "key": "existingClientReason", "input": true },
            { "type": "checkbox", "key": "eligibilityAcknowledged", "input": true },
            { "type": "radio", "key": "eligibilityCategory", "input": true },
            { "type": "textfield", "key": "socialInsuranceNumber", "input": true },
            { "type": "textfield", "key": "busPassAccountNumber", "input": true },
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "birthDay", "input": true, "validate": { "required": true } },
            { "type": "select", "key": "birthMonth", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "birthYear", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "phoneNumber", "input": true, "validate": { "required": true } },
            { "type": "select", "key": "phoneType", "input": true, "validate": { "required": true } },
            { "type": "checkbox", "key": "leaveMessage", "input": true },
            { "type": "email", "key": "email", "input": true },
            { "type": "select", "key": "preferredCommunication", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "streetAddress1", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "streetAddress2", "input": true },
            { "type": "textfield", "key": "city", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "province", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "postalCode", "input": true, "validate": { "required": true } },
            { "type": "radio", "key": "mailingAddressDifferent", "input": true },
            { "type": "textfield", "key": "mailingStreetAddress1", "input": true },
            { "type": "textfield", "key": "mailingStreetAddress2", "input": true },
            { "type": "textfield", "key": "mailingCity", "input": true },
            { "type": "textfield", "key": "mailingProvince", "input": true },
            { "type": "textfield", "key": "mailingPostalCode", "input": true },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeFormSpecProvider _specProvider = new();
        private readonly FakeBusPassSubmissionProvider _middleware = new();
        private readonly FakeCorrelationIdAccessor _correlation = new("req-123");
        private readonly FakeTimeProvider _clock = new(Now);

        /// <summary>Initializes a new instance of the <see cref="BusPassSubmissionServiceTests"/> class.</summary>
        public BusPassSubmissionServiceTests()
        {
            _specProvider.VersionResult = FakeFormSpecProvider.Spec(BusPassSubmissionService.FormSpecId, 1, Spec);
        }

        [Fact]
        public async Task Accepted_StoresTheSubmissionAndAStartedThenAcceptedEvent()
        {
            using FormsDbContext db = NewDb();
            BusPassSubmissionService service = NewService(db);

            BusPassSubmissionResultModel result = await service.SubmitAsync(Request(NewApplicant()), CancellationToken.None);

            Assert.True(result.IsValid);
            BusPassSubmissionResponseModel response = result.Response!;
            Assert.Equal(BusPassSubmissionOutcome.Accepted, response.Outcome);
            Assert.Equal("1-TEST-0001", response.ReferenceNumber);
            Assert.Null(response.Keyword);
            Assert.Equal(BusPassSubmissionService.FormSpecId, response.FormSpecId);

            FormSubmission submission = Assert.Single(await db.FormSubmissions.ToListAsync());
            Assert.Equal(response.SubmissionId, submission.Id);

            List<BusPassDispatchEvent> events = await Events(db);
            Assert.Equal(
                [BusPassDispatchEventType.Started, BusPassDispatchEventType.Accepted],
                events.Select(e => e.Type).ToArray());
            Assert.All(events, e => Assert.Equal(submission.Id, e.SubmissionId));
            Assert.All(events, e => Assert.Equal(Now, e.OccurredAt));
            Assert.Single(events.Select(e => e.AttemptId).Distinct());
            Assert.Null(events[0].ReferenceNumber);
            Assert.Equal("1-TEST-0001", events[1].ReferenceNumber);
        }

        [Fact]
        public async Task EveryEvent_CarriesTheRequestsCorrelationId()
        {
            // The same X-Request-ID that went to the middleware, so a row can be
            // matched to its trace and to the middleware's log line for the call.
            using FormsDbContext db = NewDb();
            BusPassSubmissionService service = NewService(db);

            await service.SubmitAsync(Request(NewApplicant()), CancellationToken.None);

            Assert.All(await Events(db), e => Assert.Equal("req-123", e.RequestId));
        }

        [Fact]
        public async Task WhatReachesTheMiddleware_IsMappedFromTheStoredAnswers()
        {
            using FormsDbContext db = NewDb();
            BusPassSubmissionService service = NewService(db);

            await service.SubmitAsync(Request(NewApplicant()), CancellationToken.None);

            BusPassApplicationModel sent = Assert.Single(_middleware.Submitted);
            Assert.Equal(BusPassRequestType.NewApplication, sent.RequestType);
            Assert.Equal(BusPassApplicantType.Over65, sent.ApplicantType);
            Assert.Equal("046454286", sent.SocialInsuranceNumber);
            Assert.Equal("Ada", sent.FirstName);
            Assert.Equal(new DateOnly(1950, 12, 10), sent.DateOfBirth);
            Assert.Equal("2505550199", sent.PhoneNumber);
            Assert.Equal(BusPassPhoneType.Home, sent.PhoneType);
            Assert.Equal("BC", sent.ResidentialAddress!.Province);
            Assert.Null(sent.MailingAddress);
        }

        [Fact]
        public async Task RejectedByIcm_KeepsTheSubmissionAndReportsTheRejection()
        {
            // ICM still files a numbered record for a failed match, so the
            // number comes back too; the keyword is what says it was refused.
            using FormsDbContext db = NewDb();
            _middleware.Outcome = FakeBusPassSubmissionProvider.Rejected("1-ERR-0002", "NO_MATCH", "Contact or Case Match not Found");
            BusPassSubmissionService service = NewService(db);

            BusPassSubmissionResultModel result = await service.SubmitAsync(Request(NewApplicant()), CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(BusPassSubmissionOutcome.Rejected, result.Response!.Outcome);
            Assert.Equal(BusPassErrorKeywords.Rejected, result.Response.Keyword);
            Assert.Equal("NO_MATCH", result.Response.ErrorCode);
            Assert.Equal("1-ERR-0002", result.Response.ReferenceNumber);

            List<BusPassDispatchEvent> events = await Events(db);
            Assert.Equal(BusPassDispatchEventType.Rejected, events[^1].Type);
            Assert.Equal("NO_MATCH", events[^1].ErrorCode);
            Assert.Equal("Contact or Case Match not Found", events[^1].ErrorMessage);
            Assert.Equal("1-ERR-0002", events[^1].ReferenceNumber);
            Assert.Single(await db.FormSubmissions.ToListAsync());
        }

        [Fact]
        public async Task MiddlewareUnavailable_KeepsTheSubmissionAndRecordsTheFailure()
        {
            using FormsDbContext db = NewDb();
            _middleware.Failure = new IcmApiUnavailableException("The ICM middleware could not be reached.");
            BusPassSubmissionService service = NewService(db);

            BusPassSubmissionResultModel result = await service.SubmitAsync(Request(NewApplicant()), CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(BusPassSubmissionOutcome.Failed, result.Response!.Outcome);
            Assert.Equal(BusPassErrorKeywords.IcmUnavailable, result.Response.Keyword);
            Assert.Null(result.Response.ReferenceNumber);

            Assert.Single(await db.FormSubmissions.ToListAsync());
            List<BusPassDispatchEvent> events = await Events(db);
            Assert.Equal(
                [BusPassDispatchEventType.Started, BusPassDispatchEventType.Failed],
                events.Select(e => e.Type).ToArray());
            Assert.Equal("The ICM middleware could not be reached.", events[^1].ErrorMessage);
        }

        [Fact]
        public async Task ACrashDuringTheCall_LeavesAStartedEventWithNoClosingEvent()
        {
            // Anything other than the provider's own failure type is not ours
            // to interpret. An unclosed attempt is the evidence that ICM may
            // have the request, and the reason it must never be re-sent blindly.
            using FormsDbContext db = NewDb();
            _middleware.Failure = new InvalidOperationException("IcmApi:Auth is not configured");
            BusPassSubmissionService service = NewService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SubmitAsync(Request(NewApplicant()), CancellationToken.None));

            BusPassDispatchEvent only = Assert.Single(await Events(db));
            Assert.Equal(BusPassDispatchEventType.Started, only.Type);
        }

        [Fact]
        public async Task ASecondSubmission_IsItsOwnAttempt()
        {
            using FormsDbContext db = NewDb();
            BusPassSubmissionService service = NewService(db);

            await service.SubmitAsync(Request(NewApplicant()), CancellationToken.None);
            await service.SubmitAsync(Request(NewApplicant()), CancellationToken.None);

            List<BusPassDispatchEvent> events = await Events(db);
            Assert.Equal(4, events.Count);
            Assert.Equal(2, events.Select(e => e.AttemptId).Distinct().Count());
            Assert.Equal(2, events.Select(e => e.SubmissionId).Distinct().Count());
        }

        [Fact]
        public async Task RuleFailure_StoresNothingAndSendsNothing()
        {
            using FormsDbContext db = NewDb();
            var answers = NewApplicant();
            answers.Remove("socialInsuranceNumber");
            BusPassSubmissionService service = NewService(db);

            BusPassSubmissionResultModel result = await service.SubmitAsync(Request(answers), CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Keyword == BusPassErrorKeywords.IdentifierRequired);
            Assert.Empty(await db.FormSubmissions.ToListAsync());
            Assert.Empty(await Events(db));
            Assert.Empty(_middleware.Submitted);
        }

        [Fact]
        public async Task SpecAndRuleFailures_AreReportedTogether()
        {
            using FormsDbContext db = NewDb();
            var answers = NewApplicant();
            answers.Remove("firstName");
            answers.Remove("socialInsuranceNumber");
            BusPassSubmissionService service = NewService(db);

            BusPassSubmissionResultModel result = await service.SubmitAsync(Request(answers), CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "firstName" && e.Keyword == ValidationKeywords.FieldRequired);
            Assert.Contains(result.Errors, e => e.Keyword == BusPassErrorKeywords.IdentifierRequired);
        }

        [Fact]
        public async Task UnknownSpecVersion_IsRefusedBeforeAnyRuleRuns()
        {
            using FormsDbContext db = NewDb();
            _specProvider.VersionResult = null;
            BusPassSubmissionService service = NewService(db);

            BusPassSubmissionResultModel result = await service.SubmitAsync(Request(NewApplicant(), version: 99), CancellationToken.None);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal(ValidationKeywords.VersionUnknown, error.Keyword);
            Assert.Empty(_middleware.Submitted);
        }

        private static FormSubmissionRequestModel Request(Dictionary<string, object?> answers, int version = 1)
        {
            using JsonDocument doc = JsonSerializer.SerializeToDocument(answers);
            return new FormSubmissionRequestModel { FormSpecVersion = version, Answers = doc.RootElement.Clone() };
        }

        private static Dictionary<string, object?> NewApplicant() => new()
        {
            ["applicantCategory"] = "new",
            ["eligibilityAcknowledged"] = true,
            ["eligibilityCategory"] = "over65",
            ["socialInsuranceNumber"] = "046 454 286",
            ["firstName"] = "Ada",
            ["lastName"] = "Lovelace",
            ["birthDay"] = "10",
            ["birthMonth"] = "12",
            ["birthYear"] = "1950",
            ["phoneNumber"] = "(250) 555-0199",
            ["phoneType"] = "home",
            ["preferredCommunication"] = "phone",
            ["streetAddress1"] = "501 Belleville St",
            ["city"] = "Victoria",
            ["province"] = "British Columbia",
            ["postalCode"] = "V8V 1X4",
            ["mailingAddressDifferent"] = "no",
        };

        private static FormsDbContext NewDb()
        {
            DbContextOptions<FormsDbContext> options = new DbContextOptionsBuilder<FormsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new InMemoryFormsDbContext(options);
        }

        private BusPassSubmissionService NewService(FormsDbContext db)
        {
            var forms = new FormsService(
                NullLogger<FormsService>.Instance,
                db,
                _specProvider,
                new UnexpectedPdfProvider(),
                new UnexpectedTemplateProvider());

            return new BusPassSubmissionService(
                NullLogger<BusPassSubmissionService>.Instance,
                db,
                forms,
                _middleware,
                _correlation,
                _clock);
        }

        private static Task<List<BusPassDispatchEvent>> Events(FormsDbContext db) =>
            db.BusPassDispatchEvents.OrderBy(e => e.OccurredAt).ThenBy(e => e.Type).ToListAsync();

        private sealed class UnexpectedPdfProvider : IPdfProvider
        {
            public Task<byte[]> GenerateFromOdtAsync(byte[] odtTemplate, object data, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("No PDF is rendered on submit.");
        }

        private sealed class UnexpectedTemplateProvider : ITemplateProvider
        {
            public Task<byte[]> GetTemplateAsync(string templateName, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("No template is read on submit.");
        }
    }
}
