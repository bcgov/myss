namespace Icm.Api.Tests.Repositories
{
    using System.Net;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// The prospect repository over the real Refit stack and canned responses: what ICM's
    /// status codes become for a caller.
    /// </summary>
    public class ProspectRepositoryTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static ProspectQuery Query => new() { ServiceRequestId = "1-53CJXK6" };

        private static ProspectInput Input => new()
        {
            ServiceRequestId = "1-53CJXK6",
            FirstName = "Myss",
            LastName = "IntegrationTest",
            BirthDate = new DateOnly(2000, 1, 25),
            Gender = "Man/Boy",
        };

        private static (ProspectRepository Repository, RecordingHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = "{}",
            string? trustedUserName = null)
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (new ProspectRepository(httpClient, trustedUserName), handler);
        }

        [Fact]
        public async Task SearchAsync_MapsTheMatchesIntoPublishedModels()
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """
                    {
                      "items": [
                        {
                          "Id": "1-53CJXKH",
                          "Service Request Id": "1-53CJXK6",
                          "First Name": "Myss",
                          "Applicant Flag": "N",
                          "Link": [ { "rel": "self", "href": "https://icm/x", "name": "SRProspects" } ]
                        }
                      ],
                      "Link": [ { "rel": "self", "href": "https://icm/list", "name": "SRProspects" } ]
                    }
                    """);
            handler.ResponseHeaders["Total-Record-Count"] = "1";

            ProspectPage page = await repository.SearchAsync("t", Query);

            Prospect found = Assert.Single(page.Items);
            Assert.Equal("1-53CJXKH", found.Id);
            Assert.Equal("1-53CJXK6", found.ServiceRequestId);
            Assert.False(found.IsApplicant);
            Assert.Empty(found.AdditionalFields);
            Assert.Equal(1, page.TotalCount);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task SearchAsync_TurnsNoSuchRowIntoAnEmptyPage(HttpStatusCode statusCode)
        {
            (ProspectRepository repository, _) = Create(
                statusCode,
                statusCode == HttpStatusCode.NotFound
                    ? """{"ERROR":"There is no data for the requested resource"}"""
                    : null);

            ProspectPage page = await repository.SearchAsync("t", Query);

            Assert.Empty(page.Items);
            Assert.Null(page.TotalCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{}")]
        public async Task SearchAsync_ThrowsWhenIcmClaimsSuccessButReturnsNoRows(string? responseJson)
        {
            (ProspectRepository repository, _) = Create(responseJson: responseJson);

            await Assert.ThrowsAsync<IcmResponseException>(() => repository.SearchAsync("t", Query));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task EveryCallThrowsOnARealFailure(HttpStatusCode statusCode)
        {
            (ProspectRepository repository, _) = Create(statusCode, """{"message":"nope"}""");

            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(
                () => repository.SearchAsync("t", Query))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(
                () => repository.GetAsync("t", "1-53CJXKH"))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(
                () => repository.GetForServiceRequestAsync("t", "1-53CJXK6"))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(
                () => repository.CreateAsync("t", Input))).StatusCode);
        }

        [Fact]
        public async Task GetAsync_MapsTheRow()
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """
                    {
                      "Id": "1-53CJXKH",
                      "Purpose": "Residence/Mailing",
                      "Link": [ { "rel": "self", "href": "https://icm/x", "name": "SRProspects" } ]
                    }
                    """);

            Prospect? found = await repository.GetAsync("t", "1-53CJXKH", new ProspectReadOptions { ViewMode = "Manager" });

            Assert.Equal("Residence/Mailing", found!.Purpose);
            Assert.Empty(found.AdditionalFields);
            Assert.Contains("ViewMode=Manager", handler.Request!.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task GetAsync_TurnsNoSuchRowIntoNull(HttpStatusCode statusCode)
        {
            (ProspectRepository repository, _) = Create(statusCode, null);

            Assert.Null(await repository.GetAsync("t", "1-NOPE"));
        }

        [Fact]
        public async Task GetForServiceRequestAsync_MapsTheRows()
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """
                    {
                      "lastpage": "true",
                      "items": [
                        { "Id": "1-53CJXKH", "Service Request Id": "1-53CJXK6", "Purpose": "Residence/Mailing" },
                        { "Id": "1-OTHER", "Service Request Id": "1-53CJXK6", "Purpose": "Mailing" }
                      ],
                      "Link": [ { "rel": "self", "href": "https://icm/x/SRProspects", "name": "SRProspects" } ]
                    }
                    """);

            IReadOnlyList<Prospect> rows = await repository.GetForServiceRequestAsync("t", "1-53CJXK6");

            Assert.Equal(["1-53CJXKH", "1-OTHER"], rows.Select(row => row.Id));
            Assert.Equal("Mailing", rows[1].Purpose);
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task GetForServiceRequestAsync_FollowsThePagesUntilIcmSaysLast()
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create();
            handler.NextResponses.Enqueue((HttpStatusCode.OK, """{ "lastpage": "false", "items": [ { "Id": "1-A" }, { "Id": "1-B" } ] }"""));
            handler.NextResponses.Enqueue((HttpStatusCode.OK, """{ "lastpage": "true", "items": [ { "Id": "1-C" } ] }"""));

            IReadOnlyList<Prospect> rows = await repository.GetForServiceRequestAsync("t", "1-53CJXK6");

            Assert.Equal(["1-A", "1-B", "1-C"], rows.Select(row => row.Id));
            Assert.Equal(["StartRowNum=0", "StartRowNum=2"], handler.Requests.Select(request => Parameter(request, "StartRowNum")));
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent, null)]
        [InlineData(HttpStatusCode.NotFound, """{"ERROR":"There is no data for the requested resource"}""")]
        [InlineData(HttpStatusCode.OK, """{ "lastpage": "false", "items": [] }""")]
        public async Task GetForServiceRequestAsync_StopsWhenTheRowsRunOutBeforeIcmSaysLast(
            HttpStatusCode statusCode, string? responseJson)
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create();
            handler.NextResponses.Enqueue((HttpStatusCode.OK, """{ "lastpage": "false", "items": [ { "Id": "1-A" } ] }"""));
            handler.NextResponses.Enqueue((statusCode, responseJson));

            IReadOnlyList<Prospect> rows = await repository.GetForServiceRequestAsync("t", "1-53CJXK6");

            Assert.Equal("1-A", Assert.Single(rows).Id);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task GetForServiceRequestAsync_GivesUpOnAnIcmThatNeverSaysLast()
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "lastpage": "false", "items": [ { "Id": "1-A" } ] }""");

            await Assert.ThrowsAsync<IcmResponseException>(() => repository.GetForServiceRequestAsync("t", "1-53CJXK6"));

            Assert.Equal(ProspectRepository.MaxChildPages, handler.Requests.Count);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task GetForServiceRequestAsync_TurnsNothingIntoAnEmptyList(HttpStatusCode statusCode)
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create(statusCode, null);

            Assert.Empty(await repository.GetForServiceRequestAsync("t", "1-53BUC70"));
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task CreateAsync_PutsThePlaceholderKeyedRow_AndReturnsWhatIcmStored()
        {
            // The live shape of a create's answer, MEASURED SIT2 2026-09-29 (row 1-53MU3XZ on
            // SR 1-53MU3X7): items a single row, its Link a single object.
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """
                    {
                      "items": {
                        "Service Request Id": "1-53MU3X7",
                        "First Name": "Myss",
                        "Id": "1-53MU3XZ",
                        "M/F": "Man/Boy",
                        "Birth Date": "01/25/2000",
                        "Link": { "rel": "self", "href": "https://icm/x/1-53MU3XZ", "name": "SRProspects" }
                      }
                    }
                    """);

            Prospect created = await repository.CreateAsync("t", Input);

            Assert.Equal("1-53MU3XZ", created.Id);
            Assert.Equal("1-53MU3X7", created.ServiceRequestId);
            Assert.Equal(new DateOnly(2000, 1, 25), created.BirthDate);
            Assert.Empty(created.AdditionalFields);
            Assert.Equal(HttpMethod.Put, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Prospects/SRProspects/", handler.Request.RequestUri!.AbsolutePath);
            Assert.Contains("\"Id\":\"NULL\"", handler.RequestBody, StringComparison.Ordinal);
            Assert.Contains("\"Service Request Id\":\"1-53CJXK6\"", handler.RequestBody, StringComparison.Ordinal);
            Assert.Contains("\"M/F\":\"Man/Boy\"", handler.RequestBody, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{}")]
        [InlineData("""{ "items": null }""")]
        public async Task ACreateThatClaimsSuccessWithoutTheRowIsAnError(string? responseJson)
        {
            (ProspectRepository repository, _) = Create(responseJson: responseJson);

            await Assert.ThrowsAsync<IcmResponseException>(() => repository.CreateAsync("t", Input));
        }

        [Fact]
        public async Task ARefusedCallNeverReachesIcm()
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SearchAsync("t", null!));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SearchAsync("t", new ProspectQuery()));
            await Assert.ThrowsAsync<ArgumentException>(
                () => repository.SearchAsync("t", new ProspectQuery { Source = "x\" OR [Id] LIKE \"*" }));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync("t", " "));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetForServiceRequestAsync("t", string.Empty));
            await Assert.ThrowsAsync<ArgumentNullException>(() => repository.CreateAsync("t", null!));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync("t", new ProspectInput { FirstName = "F", LastName = "L" }));

            Assert.Null(handler.Request);
        }

        [Fact]
        public async Task EveryCallSendsTheTokenAndTheTrustedUser()
        {
            (ProspectRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "items": [] }""", trustedUserName: "IDIR\\SOMEONE");

            await repository.SearchAsync("token-abc", Query);

            Assert.Equal("token-abc", handler.Request!.Headers.Authorization!.Parameter);
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request.Headers.GetValues(IServiceRequestApi.TrustedUserNameHeader)));
        }

        private static string? Parameter(HttpRequestMessage request, string name) =>
            Uri.UnescapeDataString(request.RequestUri!.Query)
                .TrimStart('?')
                .Split('&')
                .SingleOrDefault(part => part.StartsWith(name + "=", StringComparison.Ordinal));
    }
}
