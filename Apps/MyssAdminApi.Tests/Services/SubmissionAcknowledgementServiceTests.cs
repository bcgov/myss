namespace Myss.AdminApi.Tests.Services
{
    using System.Net;
    using Microsoft.Extensions.Logging;
    using Myss.AdminApi.Domain;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Services;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.Common.Auth;
    using Myss.Common.Content;
    using Myss.FormSpec;

    /// <summary>
    /// Tests for <see cref="SubmissionAcknowledgementService"/>.
    /// </summary>
    public class SubmissionAcknowledgementServiceTests
    {
        private const string FormSpecId = "bc-bus-pass";

        private readonly FakeSubmissionAcknowledgementAdminProvider _provider = new();
        private readonly CapturingLogger _logger = new();

        [Fact]
        public async Task TheFirstSave_CreatesOneEntryWithTheCallersIdirUsername_AndLogsIt()
        {
            SubmissionAcknowledgementSaveResultModel result =
                await NewService().SaveAsync(FormSpecId, Request(basedOn: null), CancellationToken.None);

            Assert.True(result.IsValid);
            (string formSpecId, SubmissionAcknowledgementTexts texts, string changedBy) = Assert.Single(_provider.Creates);
            Assert.Equal(FormSpecId, formSpecId);
            Assert.Equal(new SubmissionAcknowledgementTexts("Heading", "Title", "Body {referenceNumber}"), texts);
            Assert.Equal("MADMIN", changedBy);
            Assert.Contains(
                _logger.Entries,
                entry => entry.Level == LogLevel.Information
                    && entry.Message == "Confirmation message for bc-bus-pass published by MADMIN");
        }

        [Fact]
        public async Task ASaveBasedOnTheLiveEntry_PutsTheNewEntryFirst_AndKeepsTheHistory()
        {
            SubmissionAcknowledgementEntry older = _provider.Seed();
            SubmissionAcknowledgementEntry live = _provider.Seed("JSMITH");

            SubmissionAcknowledgementSaveResultModel result =
                await NewService().SaveAsync(FormSpecId, Request(basedOn: live.Id), CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal("MADMIN", result.Saved.Current?.ChangedBy);
            Assert.Equal(
                [result.Saved.Current!.Id, live.Id, older.Id],
                result.Saved.History.Select(entry => entry.Id));
        }

        [Fact]
        public async Task TheTexts_AreSavedWithLineFeedsAndWithoutSpacesAtEitherEnd()
        {
            SaveSubmissionAcknowledgementRequestModel request = new()
            {
                Heading = "  Heading ",
                Title = "Title\r\n",
                Body = " First line\r\nSecond {referenceNumber}\rThird ",
            };

            await NewService().SaveAsync(FormSpecId, request, CancellationToken.None);

            Assert.Equal(
                new SubmissionAcknowledgementTexts("Heading", "Title", "First line\nSecond {referenceNumber}\nThird"),
                Assert.Single(_provider.Creates).Texts);
        }

        [Fact]
        public async Task TheWrite_IsNotCancelledWithTheEditorsRequest()
        {
            using CancellationTokenSource cancelled = new();
            await cancelled.CancelAsync();

            SubmissionAcknowledgementSaveResultModel result =
                await NewService().SaveAsync(FormSpecId, Request(basedOn: null), cancelled.Token);

            Assert.True(result.IsValid);
            Assert.False(_provider.LastCreateCancellationToken.CanBeCanceled);
        }

        [Fact]
        public async Task ARefusedSave_CreatesNothing()
        {
            SaveSubmissionAcknowledgementRequestModel request = new() { Heading = " ", Title = "Title", Body = "No placeholder" };

            SubmissionAcknowledgementSaveResultModel result =
                await NewService().SaveAsync(FormSpecId, request, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.False(result.IsChanged);
            Assert.Equal(
                [SubmissionAcknowledgementKeywords.TextRequired, SubmissionAcknowledgementKeywords.ReferenceMissing],
                result.Errors.Select(error => error.Keyword));
            Assert.Empty(_provider.Creates);
        }

        [Fact]
        public async Task AStaleBasedOn_IsChanged_AndCreatesNothing()
        {
            SubmissionAcknowledgementEntry older = _provider.Seed();
            _provider.Seed("JSMITH");

            SubmissionAcknowledgementSaveResultModel result =
                await NewService().SaveAsync(FormSpecId, Request(basedOn: older.Id), CancellationToken.None);

            AssertChanged(result);
        }

        [Fact]
        public async Task ANullBasedOn_WhenAnEntryExists_IsChanged()
        {
            _provider.Seed();

            SubmissionAcknowledgementSaveResultModel result =
                await NewService().SaveAsync(FormSpecId, Request(basedOn: null), CancellationToken.None);

            AssertChanged(result);
        }

        [Fact]
        public async Task ABasedOn_WhenNoEntryExists_IsChanged()
        {
            SubmissionAcknowledgementSaveResultModel result =
                await NewService().SaveAsync(FormSpecId, Request(basedOn: "doc-gone"), CancellationToken.None);

            AssertChanged(result);
        }

        [Fact]
        public async Task AFailedWrite_IsLoggedWithTheCaller_AndRethrown()
        {
            _provider.CreateException = new ContentEngineUnavailableException("The content engine did not respond in time.");

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewService().SaveAsync(FormSpecId, Request(basedOn: null), CancellationToken.None));

            Assert.Contains(
                _logger.Entries,
                entry => entry.Level == LogLevel.Warning
                    && entry.Message == "Confirmation message for bc-bus-pass by MADMIN: the save failed or its outcome is unknown");
        }

        [Fact]
        public async Task ARefusedWrite_IsRethrown()
        {
            _provider.CreateException = new StrapiWriteException(HttpStatusCode.BadRequest, """{ "error": "invalid" }""");

            await Assert.ThrowsAsync<StrapiWriteException>(
                () => NewService().SaveAsync(FormSpecId, Request(basedOn: null), CancellationToken.None));
        }

        [Fact]
        public async Task AnUnreadableContentEngine_IsLoggedWithTheCaller_AndRethrown_WithNothingCreated()
        {
            _provider.ReadException = new ContentEngineUnavailableException("Could not read.");

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewService().SaveAsync(FormSpecId, Request(basedOn: null), CancellationToken.None));
            Assert.Empty(_provider.Creates);
            Assert.Contains(
                _logger.Entries,
                entry => entry.Level == LogLevel.Warning
                    && entry.Message == "Confirmation message for bc-bus-pass by MADMIN: the save failed or its outcome is unknown");
        }

        [Fact]
        public async Task ASaveRefusedAsChanged_IsLoggedWithTheCaller()
        {
            _provider.Seed();

            await NewService().SaveAsync(FormSpecId, Request(basedOn: null), CancellationToken.None);

            Assert.Contains(
                _logger.Entries,
                entry => entry.Level == LogLevel.Information
                    && entry.Message == "Confirmation message for bc-bus-pass not saved by MADMIN: it changed after the edit began");
        }

        [Fact]
        public async Task TheHistoryAfterASave_KeepsTheNewest20()
        {
            for (int saved = 0; saved < 20; saved++)
            {
                _provider.Seed();
            }

            SubmissionAcknowledgementSaveResultModel result =
                await NewService().SaveAsync(FormSpecId, Request(basedOn: "doc-20"), CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(20, result.Saved.History.Count);
            Assert.Equal("doc-21", result.Saved.History[0].Id);
            Assert.Equal("doc-2", result.Saved.History[^1].Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public async Task ACallerWithoutAnIdirUsername_SavesNothing(string? idirUsername)
        {
            var service = new SubmissionAcknowledgementService(_provider, new StubCurrentUser(idirUsername), _logger);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SaveAsync(FormSpecId, Request(basedOn: null), CancellationToken.None));
            Assert.Empty(_provider.Creates);
        }

        [Fact]
        public async Task Get_ReturnsTheLiveEntryAndTheHistory_NewestFirst()
        {
            SubmissionAcknowledgementEntry older = _provider.Seed();
            SubmissionAcknowledgementEntry live = _provider.Seed("JSMITH");

            SubmissionAcknowledgementAdminModel model = await NewService().GetAsync(FormSpecId, CancellationToken.None);

            Assert.Same(live, model.Current);
            Assert.Equal([live, older], model.History);
        }

        [Fact]
        public async Task Get_WithNothingSaved_HasNoLiveEntry()
        {
            SubmissionAcknowledgementAdminModel model = await NewService().GetAsync(FormSpecId, CancellationToken.None);

            Assert.Null(model.Current);
            Assert.Empty(model.History);
        }

        private static SaveSubmissionAcknowledgementRequestModel Request(string? basedOn) => new()
        {
            BasedOn = basedOn,
            Heading = "Heading",
            Title = "Title",
            Body = "Body {referenceNumber}",
        };

        private void AssertChanged(SubmissionAcknowledgementSaveResultModel result)
        {
            Assert.True(result.IsChanged);
            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("basedOn", error.Field);
            Assert.Equal(SubmissionAcknowledgementKeywords.Changed, error.Keyword);
            Assert.Empty(_provider.Creates);
        }

        private SubmissionAcknowledgementService NewService() =>
            new(_provider, new StubCurrentUser("MADMIN"), _logger);

        private sealed class StubCurrentUser(string? idirUsername) : ICurrentUserAccessor
        {
            public CurrentUser User { get; } = new() { IsAuthenticated = true, IdirUsername = idirUsername };
        }

        private sealed class CapturingLogger : ILogger<SubmissionAcknowledgementService>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
