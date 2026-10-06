namespace Myss.Api.Tests.Services
{
    using System.Net;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Tests for <see cref="EligibilityRatesService"/>.
    /// </summary>
    public class EligibilityRatesServiceTests
    {
        private const string LifecycleRefusal = """
            {
              "data": null,
              "error": {
                "status": 400,
                "name": "ApplicationError",
                "message": "Family size 2, column B must be 0 or more, not -0.01.",
                "details": { "keywords": ["RATES.AMOUNT.NEGATIVE"] }
              }
            }
            """;

        private readonly FakeEligibilityRateAdminProvider _admin = new();
        private readonly FakeEligibilityRateProvider _reader = new();
        private readonly CapturingLogger _logger = new();

        // 17:00 UTC is 10:00 in British Columbia (PDT) on 6 October 2026.
        private FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 6, 17, 0, 0, TimeSpan.Zero));

        [Fact]
        public async Task AValidTable_IsSavedAsTodaysTable_AndTheCacheIsDropped()
        {
            SaveEligibilityRatesRequestModel request = EligibilityRatesTestData.SeededRequest();

            EligibilityRatesWriteResultModel result = await NewService().SaveAsync(request, CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal("2026-10-06", result.Rates.EffectiveDate);
            Assert.Single(_admin.Saves);
            Assert.Equal("2026-10-06", _admin.Saves[0].EffectiveDate);
            Assert.Equal(request.IncomeRows, _admin.Saves[0].IncomeRows);
            Assert.Same(request.AssetLimits, _admin.Saves[0].AssetLimits);
            Assert.Equal(1, _reader.InvalidateCacheCalls);
        }

        [Fact]
        public async Task TheRows_AreSavedInFamilySizeOrder()
        {
            List<EligibilityRateRowModel> rows = EligibilityRatesTestData.SeededRows();
            rows.Reverse();
            SaveEligibilityRatesRequestModel request = new() { IncomeRows = rows, AssetLimits = EligibilityRatesTestData.SeededAssetLimits() };

            await NewService().SaveAsync(request, CancellationToken.None);

            Assert.Equal([1, 2, 3, 4, 5, 6, 7], _admin.Saves[0].IncomeRows.Select(row => row.FamilySize));
        }

        [Fact]
        public async Task TheWrite_IsNotCancelledWithTheAdminsRequest()
        {
            using CancellationTokenSource cancelled = new();
            await cancelled.CancelAsync();

            EligibilityRatesWriteResultModel result =
                await NewService().SaveAsync(EligibilityRatesTestData.SeededRequest(), cancelled.Token);

            Assert.True(result.IsValid);
            Assert.False(_admin.LastCancellationToken.CanBeCanceled);
        }

        [Fact]
        public async Task AnInvalidTable_IsRefused_WithNothingWrittenOrDropped()
        {
            List<EligibilityRateRowModel> rows = EligibilityRatesTestData.SeededRows();
            rows.RemoveAt(0);
            SaveEligibilityRatesRequestModel request = new() { IncomeRows = rows, AssetLimits = EligibilityRatesTestData.SeededAssetLimits() };

            EligibilityRatesWriteResultModel result = await NewService().SaveAsync(request, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Equal(EligibilityRateKeywords.FamilySizesInvalid, Assert.Single(result.Errors).Keyword);
            Assert.Empty(_admin.Saves);
            Assert.Equal(0, _reader.InvalidateCacheCalls);
        }

        [Theory]
        [InlineData("2026-10-03T05:30:00Z", "2026-10-02")] // 22:30 PDT the evening before
        [InlineData("2026-10-03T07:00:00Z", "2026-10-03")] // midnight PDT
        [InlineData("2026-12-01T07:30:00Z", "2026-11-30")] // 23:30 PST
        [InlineData("2026-12-01T08:00:00Z", "2026-12-01")] // midnight PST
        public async Task TheEffectiveDate_IsTodayInBritishColumbia(string utcNow, string expected)
        {
            _clock = new FakeTimeProvider(DateTimeOffset.Parse(utcNow, System.Globalization.CultureInfo.InvariantCulture));

            await NewService().SaveAsync(EligibilityRatesTestData.SeededRequest(), CancellationToken.None);

            Assert.Equal(expected, Assert.Single(_admin.Saves).EffectiveDate);
        }

        [Fact]
        public async Task ALifecycleRefusal_IsRefused_WithStrapisMessage_AndTheCacheIsKept()
        {
            _admin.SaveException = new StrapiWriteException(HttpStatusCode.BadRequest, LifecycleRefusal);

            EligibilityRatesWriteResultModel result =
                await NewService().SaveAsync(EligibilityRatesTestData.SeededRequest(), CancellationToken.None);

            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("rates", error.Field);
            Assert.Equal(EligibilityRateKeywords.ContentEngineRefused, error.Keyword);
            Assert.Equal("Family size 2, column B must be 0 or more, not -0.01.", error.Message);
            Assert.Equal(0, _reader.InvalidateCacheCalls);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("""{ "error": { "name": "ValidationError", "message": "x", "details": { "keywords": ["RATES.X"] } } }""")]
        [InlineData("""{ "error": { "name": "ApplicationError", "message": "x", "details": { "keywords": ["FORMSPEC.X"] } } }""")]
        [InlineData("""{ "error": { "name": "ApplicationError", "message": "x", "details": { "keywords": [null, 5] } } }""")]
        [InlineData("""{ "error": { "name": "ApplicationError", "message": "", "details": { "keywords": ["RATES.X"] } } }""")]
        [InlineData("""{ "error": { "name": "ApplicationError", "message": "x" } }""")]
        public async Task ABadRequestThatIsNotARateRule_Propagates(string body)
        {
            _admin.SaveException = new StrapiWriteException(HttpStatusCode.BadRequest, body);

            await Assert.ThrowsAsync<StrapiWriteException>(
                () => NewService().SaveAsync(EligibilityRatesTestData.SeededRequest(), CancellationToken.None));
            Assert.Equal(0, _reader.InvalidateCacheCalls);
        }

        [Fact]
        public async Task AForbiddenWrite_Propagates()
        {
            _admin.SaveException = new StrapiWriteException(HttpStatusCode.Forbidden, LifecycleRefusal);

            await Assert.ThrowsAsync<StrapiWriteException>(
                () => NewService().SaveAsync(EligibilityRatesTestData.SeededRequest(), CancellationToken.None));
        }

        [Fact]
        public async Task AnUnavailableContentEngine_Propagates_AfterDroppingTheCacheAndLoggingTheAttempt()
        {
            // The write may have completed in Strapi before the failure was seen.
            _admin.SaveException = new ContentEngineUnavailableException("timed out");

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewService().SaveAsync(EligibilityRatesTestData.SeededRequest(), CancellationToken.None));

            Assert.Equal(1, _reader.InvalidateCacheCalls);
            Assert.Contains(
                "Eligibility rates effective 2026-10-06 by MADMIN: the save failed or its outcome is unknown",
                _logger.Messages);
        }

        [Theory]
        [InlineData(true, "created")]
        [InlineData(false, "updated")]
        public async Task ASave_IsLoggedWithTheDateTheActionAndTheIdirUser(bool created, string action)
        {
            _admin.Created = created;

            await NewService().SaveAsync(EligibilityRatesTestData.SeededRequest(), CancellationToken.None);

            Assert.Contains($"Eligibility rates effective 2026-10-06 {action} by MADMIN", _logger.Messages);
        }

        private EligibilityRatesService NewService() =>
            new(_admin, _reader, _clock, new StubCurrentUser("MADMIN"), _logger);

        private sealed class StubCurrentUser(string idirUsername) : ICurrentUserAccessor
        {
            public CurrentUser User { get; } = new() { IsAuthenticated = true, IdirUsername = idirUsername };
        }

        private sealed class CapturingLogger : ILogger<EligibilityRatesService>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception));
        }
    }
}
