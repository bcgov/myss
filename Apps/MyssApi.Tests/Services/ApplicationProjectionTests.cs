namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Myss.Api.Intake;
    using Myss.Api.Platform;

    /// <summary>
    /// The fold is a table: same events, same state. Nothing here touches a
    /// database, which is the point of computing state instead of storing it.
    /// </summary>
    public class ApplicationProjectionTests
    {
        private static readonly DateTimeOffset When = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void AnEmptyStream_IsADraftAtVersionZero()
        {
            ApplicationState state = ApplicationProjection.Fold(EventStream.Empty);

            Assert.Equal(ApplicationStatus.Draft, state.Status);
            Assert.Equal(ApplicationStatusCodes.Draft, state.StatusCode);
            Assert.Equal(0, state.Version);
            Assert.Null(state.SubmittedAt);
            Assert.True(ApplicationProjection.CanSave(state));
            Assert.True(ApplicationProjection.CanSubmit(state));
        }

        [Fact]
        public void ASubmittedEvent_MakesItSubmittedWithThePayloadAnswers()
        {
            EventStream stream = new([Submitted(1, """{"firstName":"Ada","lastName":"Lovelace"}""")]);

            ApplicationState state = ApplicationProjection.Fold(stream);

            Assert.Equal(ApplicationStatus.Submitted, state.Status);
            Assert.Equal(ApplicationStatusCodes.Submitted, state.StatusCode);
            Assert.Equal(1, state.Version);
            Assert.Equal(When, state.SubmittedAt);
            Assert.Equal("Ada", state.Submitted!.Answers.GetProperty("firstName").GetString());
            Assert.Equal(3, state.Submitted.FormSpecVersion);
            Assert.False(ApplicationProjection.CanSave(state));
            Assert.False(ApplicationProjection.CanSubmit(state));
        }

        [Fact]
        public void AnEventThisSliceDoesNotKnow_LeavesTheStateAloneButMovesTheVersion()
        {
            // The review workflow's events land in the same stream later. Until
            // the applicant-facing fold learns them, they must not break it.
            EventStream stream = new(
            [
                Submitted(1, """{"firstName":"Ada","lastName":"Lovelace"}"""),
                new StoredEvent(2, "PickedUp", JsonDocument.Parse("""{"eventVersion":1}""").RootElement, "worker:MWORKER", When),
            ]);

            ApplicationState state = ApplicationProjection.Fold(stream);

            Assert.Equal(ApplicationStatus.Submitted, state.Status);
            Assert.Equal(2, state.Version);
        }

        [Fact]
        public void ASubmittedPayload_RoundTripsThroughTheEvent()
        {
            var payload = new SubmittedPayload
            {
                FormSpecVersion = 2,
                Answers = JsonDocument.Parse("""{"lastName":"Byron"}""").RootElement,
            };

            DomainEvent evt = payload.ToEvent();
            SubmittedPayload back = SubmittedPayload.From(evt.Payload);

            Assert.Equal(ApplicationEventTypes.Submitted, evt.Type);
            Assert.Equal(SubmittedPayload.CurrentEventVersion, evt.Payload.GetProperty("eventVersion").GetInt32());
            Assert.Equal(2, back.FormSpecVersion);
            Assert.Equal("Byron", back.Answers.GetProperty("lastName").GetString());
        }

        private static StoredEvent Submitted(int version, string answersJson)
        {
            var payload = new SubmittedPayload
            {
                FormSpecVersion = 3,
                Answers = JsonDocument.Parse(answersJson).RootElement,
            };
            return new StoredEvent(version, ApplicationEventTypes.Submitted, payload.ToEvent().Payload, "applicant:mock-alice", When);
        }
    }
}
