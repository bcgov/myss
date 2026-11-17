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
    /// Tests for <see cref="FormMessageService"/>.
    /// </summary>
    public class FormMessageServiceTests
    {
        private static readonly FormMessageDefinition BusPass = FormMessageDefinitions.Find("bc-bus-pass", "confirmation")!;

        private readonly FakeFormMessageAdminProvider _provider = new();
        private readonly CapturingLogger _logger = new();

        [Fact]
        public async Task TheFirstSave_CreatesOneEntryWithTheCallersIdirUsername_AndLogsIt()
        {
            FormMessageSaveResultModel result =
                await NewService().SaveAsync(BusPass, Request(basedOn: null), CancellationToken.None);

            Assert.True(result.IsValid);
            (string formSpecId, string messageKey, FormMessageTexts texts, string changedBy) = Assert.Single(_provider.Creates);
            Assert.Equal("bc-bus-pass", formSpecId);
            Assert.Equal("confirmation", messageKey);
            Assert.Equal(new FormMessageTexts("Heading", "Title", "Body {referenceNumber}"), texts);
            Assert.Equal("MADMIN", changedBy);
            Assert.Contains(
                _logger.Entries,
                entry => entry.Level == LogLevel.Information
                    && entry.Message == "Form message confirmation for bc-bus-pass published by MADMIN");
        }

        [Fact]
        public async Task ASaveBasedOnTheLiveEntry_PutsTheNewEntryFirst_AndKeepsTheHistory()
        {
            FormMessageEntry older = _provider.Seed();
            FormMessageEntry live = _provider.Seed("JSMITH");

            FormMessageSaveResultModel result =
                await NewService().SaveAsync(BusPass, Request(basedOn: live.Id), CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal("MADMIN", result.Saved.Current?.ChangedBy);
            Assert.Equal(
                [result.Saved.Current!.Id, live.Id, older.Id],
                result.Saved.History.Select(entry => entry.Id));
        }

        [Fact]
        public async Task TheTexts_AreSavedWithLineFeedsAndWithoutSpacesAtEitherEnd()
        {
            SaveFormMessageRequestModel request = new()
            {
                Heading = "  Heading ",
                Title = "Title\r\n",
                Body = " First line\r\nSecond {referenceNumber}\rThird ",
            };

            await NewService().SaveAsync(BusPass, request, CancellationToken.None);

            Assert.Equal(
                new FormMessageTexts("Heading", "Title", "First line\nSecond {referenceNumber}\nThird"),
                Assert.Single(_provider.Creates).Texts);
        }

        [Fact]
        public async Task TheWrite_IsNotCancelledWithTheEditorsRequest()
        {
            using CancellationTokenSource cancelled = new();
            await cancelled.CancelAsync();

            FormMessageSaveResultModel result =
                await NewService().SaveAsync(BusPass, Request(basedOn: null), cancelled.Token);

            Assert.True(result.IsValid);
            Assert.False(_provider.LastCreateCancellationToken.CanBeCanceled);
        }

        [Fact]
        public async Task ARefusedSave_CreatesNothing()
        {
            SaveFormMessageRequestModel request = new() { Heading = " ", Title = "Title", Body = "No placeholder" };

            FormMessageSaveResultModel result =
                await NewService().SaveAsync(BusPass, request, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.False(result.IsChanged);
            Assert.Equal(
                [FormMessageKeywords.TextRequired, FormMessageKeywords.PlaceholderMissing],
                result.Errors.Select(error => error.Keyword));
            Assert.Empty(_provider.Creates);
        }

        [Fact]
        public async Task AStaleBasedOn_IsChanged_AndCreatesNothing()
        {
            FormMessageEntry older = _provider.Seed();
            _provider.Seed("JSMITH");

            FormMessageSaveResultModel result =
                await NewService().SaveAsync(BusPass, Request(basedOn: older.Id), CancellationToken.None);

            AssertChanged(result);
        }

        [Fact]
        public async Task ANullBasedOn_WhenAnEntryExists_IsChanged()
        {
            _provider.Seed();

            FormMessageSaveResultModel result =
                await NewService().SaveAsync(BusPass, Request(basedOn: null), CancellationToken.None);

            AssertChanged(result);
        }

        [Fact]
        public async Task ABasedOn_WhenNoEntryExists_IsChanged()
        {
            FormMessageSaveResultModel result =
                await NewService().SaveAsync(BusPass, Request(basedOn: "doc-gone"), CancellationToken.None);

            AssertChanged(result);
        }

        [Fact]
        public async Task AFailedWrite_IsLoggedWithTheCaller_AndRethrown()
        {
            _provider.CreateException = new ContentEngineUnavailableException("The content engine did not respond in time.");

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewService().SaveAsync(BusPass, Request(basedOn: null), CancellationToken.None));

            Assert.Contains(
                _logger.Entries,
                entry => entry.Level == LogLevel.Warning
                    && entry.Message == "Form message confirmation for bc-bus-pass by MADMIN: the save failed or its outcome is unknown");
        }

        [Fact]
        public async Task ARefusedWrite_IsRethrown()
        {
            _provider.CreateException = new StrapiWriteException(HttpStatusCode.BadRequest, """{ "error": "invalid" }""");

            await Assert.ThrowsAsync<StrapiWriteException>(
                () => NewService().SaveAsync(BusPass, Request(basedOn: null), CancellationToken.None));
        }

        [Fact]
        public async Task AnUnreadableContentEngine_IsLoggedWithTheCaller_AndRethrown_WithNothingCreated()
        {
            _provider.ReadException = new ContentEngineUnavailableException("Could not read.");

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewService().SaveAsync(BusPass, Request(basedOn: null), CancellationToken.None));
            Assert.Empty(_provider.Creates);
            Assert.Contains(
                _logger.Entries,
                entry => entry.Level == LogLevel.Warning
                    && entry.Message == "Form message confirmation for bc-bus-pass by MADMIN: the save failed or its outcome is unknown");
        }

        [Fact]
        public async Task ASaveRefusedAsChanged_IsLoggedWithTheCaller()
        {
            _provider.Seed();

            await NewService().SaveAsync(BusPass, Request(basedOn: null), CancellationToken.None);

            Assert.Contains(
                _logger.Entries,
                entry => entry.Level == LogLevel.Information
                    && entry.Message == "Form message confirmation for bc-bus-pass not saved by MADMIN: it changed after the edit began");
        }

        [Fact]
        public async Task TheHistoryAfterASave_KeepsTheNewest20()
        {
            for (int saved = 0; saved < 20; saved++)
            {
                _provider.Seed();
            }

            FormMessageSaveResultModel result =
                await NewService().SaveAsync(BusPass, Request(basedOn: "doc-20"), CancellationToken.None);

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
            var service = new FormMessageService(_provider, new StubCurrentUser(idirUsername), _logger);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SaveAsync(BusPass, Request(basedOn: null), CancellationToken.None));
            Assert.Empty(_provider.Creates);
        }

        [Fact]
        public async Task Get_ReturnsTheLiveEntryAndTheHistory_NewestFirst()
        {
            FormMessageEntry older = _provider.Seed();
            FormMessageEntry live = _provider.Seed("JSMITH");

            FormMessageAdminModel model = await NewService().GetAsync(BusPass, CancellationToken.None);

            Assert.Same(live, model.Current);
            Assert.Equal([live, older], model.History);
        }

        [Fact]
        public async Task Get_WithNothingSaved_HasNoLiveEntry()
        {
            FormMessageAdminModel model = await NewService().GetAsync(BusPass, CancellationToken.None);

            Assert.Null(model.Current);
            Assert.Empty(model.History);
        }

        [Fact]
        public async Task Get_ReadsOnlyThisMessage()
        {
            _provider.Seed(messageKey: "intro");

            FormMessageAdminModel model = await NewService().GetAsync(BusPass, CancellationToken.None);

            Assert.Same(BusPass, model.Definition);
            Assert.Empty(model.History);
            Assert.Equal(("bc-bus-pass", "confirmation", 20), Assert.Single(_provider.Reads));
        }

        [Fact]
        public async Task List_GivesEachDefinedMessage_WithItsLastChange()
        {
            _provider.Seed();
            FormMessageEntry live = _provider.Seed("JSMITH");

            IReadOnlyList<FormMessageSummaryModel> summaries = await NewService().ListAsync(CancellationToken.None);

            Assert.Equal(FormMessageDefinitions.All, summaries.Select(summary => summary.Definition));
            FormMessageSummaryModel busPass = Assert.Single(summaries, summary => summary.Definition == BusPass);
            Assert.Equal(live.ChangedAt, busPass.LastChangedAt);
            Assert.Equal("JSMITH", busPass.LastChangedBy);
            Assert.Contains(("bc-bus-pass", "confirmation", 5), _provider.Reads);
        }

        [Fact]
        public async Task List_WithNothingSaved_HasNoLastChange()
        {
            IReadOnlyList<FormMessageSummaryModel> summaries = await NewService().ListAsync(CancellationToken.None);

            FormMessageSummaryModel busPass = Assert.Single(summaries, summary => summary.Definition == BusPass);
            Assert.Null(busPass.LastChangedAt);
            Assert.Null(busPass.LastChangedBy);
        }

        [Fact]
        public async Task List_AnUnreadableContentEngine_Throws()
        {
            _provider.ReadException = new ContentEngineUnavailableException("Could not read.");

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => NewService().ListAsync(CancellationToken.None));
        }

        private static SaveFormMessageRequestModel Request(string? basedOn) => new()
        {
            BasedOn = basedOn,
            Heading = "Heading",
            Title = "Title",
            Body = "Body {referenceNumber}",
        };

        private void AssertChanged(FormMessageSaveResultModel result)
        {
            Assert.True(result.IsChanged);
            ValidationErrorModel error = Assert.Single(result.Errors);
            Assert.Equal("basedOn", error.Field);
            Assert.Equal(FormMessageKeywords.Changed, error.Keyword);
            Assert.Empty(_provider.Creates);
        }

        private FormMessageService NewService() =>
            new(_provider, new StubCurrentUser("MADMIN"), _logger);

        private sealed class StubCurrentUser(string? idirUsername) : ICurrentUserAccessor
        {
            public CurrentUser User { get; } = new() { IsAuthenticated = true, IdirUsername = idirUsername };
        }

        private sealed class CapturingLogger : ILogger<FormMessageService>
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
