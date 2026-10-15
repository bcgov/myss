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
        public void AnEventTheFoldDoesNotKnow_LeavesTheStateAloneButMovesTheVersion()
        {
            EventStream stream = new(
            [
                Submitted(1, """{"firstName":"Ada","lastName":"Lovelace"}"""),
                new StoredEvent(2, "SomethingLater", JsonDocument.Parse("""{"eventVersion":1}""").RootElement, "worker:MWORKER", When),
            ]);

            ApplicationState state = ApplicationProjection.Fold(stream);

            Assert.Equal(ApplicationStatus.Submitted, state.Status);
            Assert.Equal(2, state.Version);
        }

        [Fact]
        public void APickUp_PutsItUnderReviewAssignedToThatWorker()
        {
            ApplicationState state = ApplicationProjection.Fold(new([Ada(1), Marker(2, ApplicationEventTypes.PickedUp)]));

            Assert.Equal(ApplicationStatus.UnderReview, state.Status);
            Assert.Equal(ApplicationStatusCodes.UnderReview, state.StatusCode);
            Assert.Equal("MWORKER", state.Assignee);
            Assert.Equal(2, state.Version);
            Assert.False(ApplicationProjection.IsInQueue(state));
            Assert.False(ApplicationProjection.CanSave(state));
        }

        [Fact]
        public void TheFirstSlicesReviewStarted_FoldsAsAPickUpByItsActor()
        {
            // Files reviewed before assignment existed keep a sensible state.
            ApplicationState state = ApplicationProjection.Fold(new([Ada(1), Marker(2, ApplicationEventTypes.ReviewStarted)]));

            Assert.Equal(ApplicationStatus.UnderReview, state.Status);
            Assert.Equal("MWORKER", state.Assignee);
        }

        [Fact]
        public void TheDecisions_FoldInOrderClearTheAssigneeAndKeepTheSubmittedAnswers()
        {
            StoredEvent submitted = Ada(1);
            StoredEvent pickUp = Marker(2, ApplicationEventTypes.PickedUp);

            ApplicationState accepted = ApplicationProjection.Fold(new([submitted, pickUp, Marker(3, ApplicationEventTypes.Accepted)]));
            ApplicationState denied = ApplicationProjection.Fold(new([submitted, pickUp, Marker(3, ApplicationEventTypes.Denied)]));
            ApplicationState escalated = ApplicationProjection.Fold(new([submitted, pickUp, Marker(3, ApplicationEventTypes.Escalated)]));
            ApplicationState returned = ApplicationProjection.Fold(new([submitted, pickUp, Marker(3, ApplicationEventTypes.MoreInfoRequested)]));

            Assert.Equal(ApplicationStatusCodes.Accepted, accepted.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Denied, denied.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Escalated, escalated.StatusCode);
            Assert.Equal(ApplicationStatusCodes.Returned, returned.StatusCode);
            Assert.All([accepted, denied, escalated, returned], s => Assert.Null(s.Assignee));
            Assert.All([accepted, denied, escalated, returned], s => Assert.Equal("Ada", s.Submitted!.Answers.GetProperty("firstName").GetString()));
            Assert.All([accepted, denied, escalated, returned], s => Assert.Equal(When, s.SubmittedAt));
            Assert.Equal(When, returned.ReturnedAt);
        }

        [Fact]
        public void AReturn_IsEditableAgainAndInTheQueue_AndAResubmitTakesTheNewAnswers()
        {
            StoredEvent[] returned = [Ada(1), Marker(2, ApplicationEventTypes.PickedUp), Marker(3, ApplicationEventTypes.MoreInfoRequested)];
            ApplicationState atReturn = ApplicationProjection.Fold(new(returned));
            ApplicationState resubmitted = ApplicationProjection.Fold(new([.. returned, Submission(4, ApplicationEventTypes.Resubmitted, """{"firstName":"Augusta","lastName":"King"}""")]));

            Assert.True(ApplicationProjection.CanSave(atReturn));
            Assert.True(ApplicationProjection.CanSubmit(atReturn));
            Assert.True(ApplicationProjection.IsInQueue(atReturn));
            Assert.Empty(ApplicationProjection.AvailableWorkerActions(atReturn, "MWORKER"));
            Assert.Equal(ApplicationEventTypes.Resubmitted, ApplicationProjection.SubmitEventFor(atReturn));
            Assert.Equal(ApplicationEventTypes.Submitted, ApplicationProjection.SubmitEventFor(ApplicationState.Seed));

            Assert.Equal(ApplicationStatusCodes.Resubmitted, resubmitted.StatusCode);
            Assert.Equal("Augusta", resubmitted.Submitted!.Answers.GetProperty("firstName").GetString());
            Assert.Equal(When, resubmitted.SubmittedAt);
            Assert.False(ApplicationProjection.CanSave(resubmitted));
            Assert.True(ApplicationProjection.IsInQueue(resubmitted));
            Assert.Equal([WorkerActions.Review], ApplicationProjection.AvailableWorkerActions(resubmitted, "MWORKER"));
        }

        [Fact]
        public void ARelease_PutsItBackAsItWasQueued()
        {
            StoredEvent pickUp = Marker(2, ApplicationEventTypes.PickedUp);
            ApplicationState fromSubmitted = ApplicationProjection.Fold(new([Ada(1), pickUp, Marker(3, ApplicationEventTypes.Released)]));
            ApplicationState fromResubmitted = ApplicationProjection.Fold(new(
            [
                Ada(1),
                pickUp,
                Marker(3, ApplicationEventTypes.MoreInfoRequested),
                Submission(4, ApplicationEventTypes.Resubmitted, """{"firstName":"Ada","lastName":"King"}"""),
                Marker(5, ApplicationEventTypes.PickedUp),
                Marker(6, ApplicationEventTypes.Released),
            ]));

            Assert.Equal(ApplicationStatus.Submitted, fromSubmitted.Status);
            Assert.Null(fromSubmitted.Assignee);
            Assert.Equal(ApplicationStatus.Resubmitted, fromResubmitted.Status);
        }

        [Fact]
        public void PromotionOutcomes_KeepAcceptedOrMarkTheFailure()
        {
            StoredEvent[] accepted = [Ada(1), Marker(2, ApplicationEventTypes.PickedUp), Marker(3, ApplicationEventTypes.Accepted)];
            ApplicationState promoted = ApplicationProjection.Fold(new([.. accepted, Marker(4, ApplicationEventTypes.Promoted, "system:promotion")]));
            ApplicationState failed = ApplicationProjection.Fold(new(
            [
                .. accepted,
                new StoredEvent(4, ApplicationEventTypes.PromotionFailed, new PromotionFailedPayload { Reason = "Down." }.ToEvent().Payload, "system:promotion", When),
            ]));

            Assert.Equal(ApplicationStatusCodes.Accepted, promoted.StatusCode);
            Assert.Equal(ApplicationStatusCodes.PromotionFailed, failed.StatusCode);
            Assert.Empty(ApplicationProjection.AvailableWorkerActions(failed, "MWORKER"));
            Assert.Equal(ApplicationHistoryCodes.PromotionFailed, failed.History[^1].Code);
        }

        [Fact]
        public void Notes_AreKeptInOrderAndLeaveTheStatusAlone()
        {
            ApplicationState state = ApplicationProjection.Fold(new(
            [
                Ada(1),
                Marker(2, ApplicationEventTypes.PickedUp),
                Note(3, "Called the applicant."),
                Note(4, "Waiting on a document."),
            ]));

            Assert.Equal(ApplicationStatus.UnderReview, state.Status);
            Assert.Equal(["Called the applicant.", "Waiting on a document."], state.Notes.Select(n => n.Text));
            Assert.All(state.Notes, n => Assert.Equal("MWORKER", n.AuthorIdir));
            Assert.DoesNotContain(state.History, h => h.Code == "NOTE");
            Assert.Equal(4, state.Version);
        }

        [Fact]
        public void TheHistory_RecordsEachMoveWithItsCodeAndActor()
        {
            ApplicationState state = ApplicationProjection.Fold(new(
            [
                Ada(1),
                Marker(2, ApplicationEventTypes.PickedUp),
                Marker(3, ApplicationEventTypes.MoreInfoRequested),
                Submission(4, ApplicationEventTypes.Resubmitted, """{"firstName":"Ada","lastName":"King"}"""),
                Marker(5, ApplicationEventTypes.PickedUp),
                Marker(6, ApplicationEventTypes.Escalated),
            ]));

            Assert.Equal(
                [
                    ApplicationHistoryCodes.Submit,
                    ApplicationHistoryCodes.PickUp,
                    ApplicationHistoryCodes.RequestMoreInfo,
                    ApplicationHistoryCodes.Resubmit,
                    ApplicationHistoryCodes.PickUp,
                    ApplicationHistoryCodes.Escalate,
                ],
                state.History.Select(h => h.Code));
            Assert.Equal("applicant:mock-alice", state.History[0].Actor);
            Assert.Equal("worker:MWORKER", state.History[^1].Actor);
        }

        [Fact]
        public void WorkerActions_AreComputedFromStateAndTheCaller()
        {
            StoredEvent submitted = Ada(1);
            ApplicationState underReview = ApplicationProjection.Fold(new([submitted, Marker(2, ApplicationEventTypes.PickedUp)]));

            Assert.Empty(ApplicationProjection.AvailableWorkerActions(ApplicationState.Seed, "MWORKER"));
            Assert.Equal([WorkerActions.Review], ApplicationProjection.AvailableWorkerActions(ApplicationProjection.Fold(new([submitted])), "MWORKER"));
            Assert.Equal(
                [WorkerActions.RequestMoreInfo, WorkerActions.Escalate, WorkerActions.Deny, WorkerActions.Accept, WorkerActions.Release],
                ApplicationProjection.AvailableWorkerActions(underReview, "MWORKER"));
            Assert.Equal(
                ApplicationProjection.AvailableWorkerActions(underReview, "MWORKER"),
                ApplicationProjection.AvailableWorkerActions(underReview, "mworker"));
            Assert.Empty(ApplicationProjection.AvailableWorkerActions(underReview, "MADMIN"));
            Assert.True(ApplicationProjection.CanAddNote(underReview, "MWORKER"));
            Assert.False(ApplicationProjection.CanAddNote(underReview, "MADMIN"));
            Assert.True(ApplicationProjection.IsAssignedToAnother(underReview, "MADMIN"));
            Assert.False(ApplicationProjection.IsAssignedToAnother(underReview, "MWORKER"));
            foreach (string terminal in new[] { ApplicationEventTypes.Accepted, ApplicationEventTypes.Denied, ApplicationEventTypes.Escalated })
            {
                ApplicationState decided = ApplicationProjection.Fold(new([submitted, Marker(2, ApplicationEventTypes.PickedUp), Marker(3, terminal)]));
                Assert.Empty(ApplicationProjection.AvailableWorkerActions(decided, "MWORKER"));
                Assert.False(ApplicationProjection.CanAddNote(decided, "MWORKER"));
            }
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

        private static StoredEvent Marker(int version, string type, string actor = "worker:MWORKER")
        {
            return new StoredEvent(version, type, new MarkerPayload().ToEvent(type).Payload, actor, When);
        }

        private static StoredEvent Note(int version, string text)
        {
            return new StoredEvent(version, ApplicationEventTypes.NoteAdded, new NotePayload { Text = text }.ToEvent().Payload, "worker:MWORKER", When);
        }

        private static StoredEvent Ada(int version)
        {
            return Submitted(version, """{"firstName":"Ada","lastName":"Lovelace"}""");
        }

        private static StoredEvent Submitted(int version, string answersJson)
        {
            return Submission(version, ApplicationEventTypes.Submitted, answersJson);
        }

        private static StoredEvent Submission(int version, string type, string answersJson)
        {
            var payload = new SubmittedPayload
            {
                FormSpecVersion = 3,
                Answers = JsonDocument.Parse(answersJson).RootElement,
            };
            return new StoredEvent(version, type, payload.ToEvent(type).Payload, "applicant:mock-alice", When);
        }
    }
}
