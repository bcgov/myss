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
        public void TheWorkerMoves_FoldInOrderAndKeepTheSubmittedAnswers()
        {
            StoredEvent submitted = Submitted(1, """{"firstName":"Ada","lastName":"Lovelace"}""");
            StoredEvent review = Marker(2, ApplicationEventTypes.ReviewStarted);

            ApplicationState underReview = ApplicationProjection.Fold(new([submitted, review]));
            ApplicationState accepted = ApplicationProjection.Fold(new([submitted, review, Marker(3, ApplicationEventTypes.Accepted)]));
            ApplicationState denied = ApplicationProjection.Fold(new([submitted, review, Marker(3, ApplicationEventTypes.Denied)]));

            Assert.Equal(ApplicationStatus.UnderReview, underReview.Status);
            Assert.Equal(ApplicationStatusCodes.UnderReview, underReview.StatusCode);
            Assert.Equal(2, underReview.Version);
            Assert.Equal(ApplicationStatus.Accepted, accepted.Status);
            Assert.Equal(ApplicationStatus.Denied, denied.Status);
            Assert.Equal(3, denied.Version);
            Assert.Equal(When, denied.SubmittedAt);
            Assert.Equal("Ada", denied.Submitted!.Answers.GetProperty("firstName").GetString());
            Assert.False(ApplicationProjection.CanSave(underReview));
            Assert.False(ApplicationProjection.CanSubmit(accepted));
        }

        [Fact]
        public void WorkerActions_AreComputedFromState()
        {
            StoredEvent submitted = Submitted(1, """{"firstName":"Ada","lastName":"Lovelace"}""");

            Assert.Empty(ApplicationProjection.AvailableWorkerActions(ApplicationState.Seed));
            Assert.Equal([WorkerActions.Review], ApplicationProjection.AvailableWorkerActions(ApplicationProjection.Fold(new([submitted]))));
            Assert.Equal(
                [WorkerActions.Accept, WorkerActions.Deny],
                ApplicationProjection.AvailableWorkerActions(ApplicationProjection.Fold(new([submitted, Marker(2, ApplicationEventTypes.ReviewStarted)]))));
            Assert.Empty(ApplicationProjection.AvailableWorkerActions(ApplicationProjection.Fold(new([submitted, Marker(2, ApplicationEventTypes.ReviewStarted), Marker(3, ApplicationEventTypes.Accepted)]))));
            Assert.Empty(ApplicationProjection.AvailableWorkerActions(ApplicationProjection.Fold(new([submitted, Marker(2, ApplicationEventTypes.ReviewStarted), Marker(3, ApplicationEventTypes.Denied)]))));
        }

        [Fact]
        public void TheReferenceNumber_IsDerivedFromTheIdAndStable()
        {
            var id = Guid.Parse("3f9a2c1b-0000-4000-8000-000000000000");

            Assert.Equal("IA-3F9A2C1B", ApplicationReference.From(id));
            Assert.Equal(ApplicationReference.From(id), ApplicationReference.From(id));
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

        private static StoredEvent Marker(int version, string type)
        {
            return new StoredEvent(version, type, new MarkerPayload().ToEvent(type).Payload, "worker:MWORKER", When);
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
