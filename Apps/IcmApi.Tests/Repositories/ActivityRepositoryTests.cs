namespace Icm.Api.Tests.Repositories
{
    using System.Net;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>The activity repository over the real Refit stack and canned responses.</summary>
    public class ActivityRepositoryTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static ActivityQuery Query => new() { ServiceRequestId = "1-53CJXO6" };

        private static ActivityInput Input => new() { ServiceRequestId = "1-53MU3X7", Type = "Task", ActionBy = "Staff" };

        private static (ActivityRepository Repository, RecordingHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK, string? responseJson = "{}", string? trustedUserName = null)
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (new ActivityRepository(httpClient, trustedUserName), handler);
        }

        [Fact]
        public async Task SearchAsync_MapsTheMatches_AndTheCount()
        {
            (ActivityRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "items": [ { "Id": "1-53CJXOB", "Activity SR Id": "1-53CJXO6", "Plan Name": "Bus Pass - New Intake", "Done Flag": "N" } ] }""");
            handler.ResponseHeaders["Total-Record-Count"] = "1";

            ActivityPage page = await repository.SearchAsync("t", Query);

            Activity found = Assert.Single(page.Items);
            Assert.Equal("Bus Pass - New Intake", found.PlanName);
            Assert.False(found.IsDone);
            Assert.Equal(1, page.TotalCount);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task NothingFoundIsEmpty_OnEveryRead(HttpStatusCode statusCode)
        {
            (ActivityRepository repository, _) = Create(statusCode, null);

            Assert.Empty((await repository.SearchAsync("t", Query)).Items);
            Assert.Null(await repository.GetAsync("t", "1-NOPE"));
            Assert.Empty(await repository.GetForServiceRequestAsync("t", "1-53MU3X7"));
            Assert.Empty((await repository.SearchPlansAsync("t", new ActivityPlanQuery { ServiceRequestId = "1-X" })).Items);
            Assert.Empty(await repository.GetPlansForServiceRequestAsync("t", "1-53MU3X7"));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task EveryCallThrowsOnARealFailure(HttpStatusCode statusCode)
        {
            (ActivityRepository repository, _) = Create(statusCode, """{"message":"nope"}""");

            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync("t", Query))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(() => repository.GetAsync("t", "1-X"))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(() => repository.GetForServiceRequestAsync("t", "1-X"))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(() => repository.CreateAsync("t", Input))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(() => repository.SearchPlansAsync("t", new ActivityPlanQuery { Id = "1-X" }))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(() => repository.GetPlansForServiceRequestAsync("t", "1-X"))).StatusCode);
        }

        [Fact]
        public async Task GetForServiceRequestAsync_FollowsThePagesUntilIcmSaysLast()
        {
            (ActivityRepository repository, RecordingHttpMessageHandler handler) = Create();
            handler.NextResponses.Enqueue((HttpStatusCode.OK, """{ "lastpage": "false", "items": [ { "Id": "1-A", "Sequence": "01" } ] }"""));
            handler.NextResponses.Enqueue((HttpStatusCode.OK, """{ "lastpage": "true", "items": [ { "Id": "1-B", "Sequence": "01-01" } ] }"""));

            IReadOnlyList<Activity> rows = await repository.GetForServiceRequestAsync("t", "1-53CJXO6");

            Assert.Equal(["01", "01-01"], rows.Select(row => row.Sequence));
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task GetForServiceRequestAsync_GivesUpOnAnIcmThatNeverSaysLast()
        {
            (ActivityRepository repository, RecordingHttpMessageHandler handler) = Create(responseJson: """{ "lastpage": "false", "items": [ { "Id": "1-A" } ] }""");

            await Assert.ThrowsAsync<IcmResponseException>(() => repository.GetForServiceRequestAsync("t", "1-53CJXO6"));
            Assert.Equal(ActivityRepository.MaxChildPages, handler.Requests.Count);
        }

        [Fact]
        public async Task GetPlansForServiceRequestAsync_MapsTheRows()
        {
            (ActivityRepository repository, _) = Create(responseJson: """
                { "lastpage": "true", "items": [ { "Id": "1-53CJXO9", "Template": "Bus Pass - New Intake", "Template Id": "1-30LDVGD", "Status": "In Progress" } ] }
                """);

            ActivityPlan plan = Assert.Single(await repository.GetPlansForServiceRequestAsync("t", "1-53CJXO6"));
            Assert.Equal("1-30LDVGD", plan.TemplateId);
            Assert.Equal("In Progress", plan.Status);
        }

        [Fact]
        public async Task CreateAsync_PutsThePlaceholderKeyedRow_AndReturnsWhatIcmStored()
        {
            // The live shape, MEASURED SIT2 2026-09-29 (1-53MU3Y4 on SR 1-53MU3X7).
            (ActivityRepository repository, RecordingHttpMessageHandler handler) = Create(responseJson: """
                { "items": { "Id": "1-53MU3Y4", "Activity SR Id": "1-53MU3X7", "Type": "Task", "Sequence": "01", "Link": { "rel": "self", "href": "https://icm/x", "name": "Activities" } } }
                """);

            Activity created = await repository.CreateAsync("t", Input);

            Assert.Equal("1-53MU3Y4", created.Id);
            Assert.Equal("01", created.Sequence);
            Assert.Equal(HttpMethod.Put, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Activities/Activities/", handler.Request.RequestUri!.AbsolutePath);
            Assert.Contains("\"Id\":\"NULL\"", handler.RequestBody, StringComparison.Ordinal);
            Assert.Contains("\"Category\":\"General\"", handler.RequestBody, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{}")]
        public async Task ACreateThatClaimsSuccessWithoutTheRowIsAnError(string? responseJson)
        {
            (ActivityRepository repository, _) = Create(responseJson: responseJson);
            await Assert.ThrowsAsync<IcmResponseException>(() => repository.CreateAsync("t", Input));
        }

        [Fact]
        public async Task ARefusedCallNeverReachesIcm()
        {
            (ActivityRepository repository, RecordingHttpMessageHandler handler) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SearchAsync("t", null!));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SearchAsync("t", new ActivityQuery()));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync("t", " "));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetForServiceRequestAsync("t", string.Empty));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync("t", new ActivityInput { Type = "Task" }));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SearchPlansAsync("t", new ActivityPlanQuery()));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetPlansForServiceRequestAsync("t", " "));

            Assert.Null(handler.Request);
        }
    }
}
