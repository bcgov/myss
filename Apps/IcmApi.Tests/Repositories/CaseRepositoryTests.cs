namespace Icm.Api.Tests.Repositories
{
    using System.Net;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// The case repository over the real Refit stack and canned responses: what ICM's
    /// status codes become for a caller.
    /// </summary>
    public class CaseRepositoryTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static CaseQuery Query => new() { KeyPlayerContactId = "1-532MU4J" };

        private static (CaseRepository Repository, RecordingHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = "{}",
            string? trustedUserName = null)
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (new CaseRepository(httpClient, trustedUserName), handler);
        }

        [Fact]
        public async Task SearchAsync_MapsTheMatchesIntoPublishedModels()
        {
            // The live shape, MEASURED 2026-09-24: items is an array (uniformresponse=Y)
            // and each record carries self/canonical links.
            (CaseRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """
                    {
                      "items": [
                        {
                          "Id": "1-5371KIQ",
                          "Case Num": "1-11077140770",
                          "Status": "Open",
                          "Restricted Flag": "N",
                          "Key Player Id": "1-532MU4J",
                          "Link": [ { "rel": "self", "href": "https://icm/x", "name": "Case" } ]
                        }
                      ],
                      "Link": [ { "rel": "self", "href": "https://icm/list", "name": "Case" } ]
                    }
                    """);
            handler.ResponseHeaders["Total-Record-Count"] = "1";

            CasePage page = await repository.SearchAsync("t", Query);

            Case found = Assert.Single(page.Items);
            Assert.Equal("1-5371KIQ", found.Id);
            Assert.Equal("1-11077140770", found.CaseNumber);
            Assert.False(found.IsRestricted);
            Assert.Empty(found.AdditionalFields);
            Assert.Equal(1, page.TotalCount);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task SearchAsync_TurnsNoSuchCaseIntoAnEmptyPage(HttpStatusCode statusCode)
        {
            // The 404 is what ICM sends (MEASURED 2026-09-24) — for no match and for a
            // case the ViewMode hides alike; the 204 is documented.
            (CaseRepository repository, _) = Create(
                statusCode,
                statusCode == HttpStatusCode.NotFound
                    ? """{"ERROR":"There is no data for the requested resource"}"""
                    : null);

            CasePage page = await repository.SearchAsync("t", Query);

            Assert.Empty(page.Items);
            Assert.Null(page.TotalCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{}")]
        public async Task SearchAsync_ThrowsWhenIcmClaimsSuccessButReturnsNoRecords(string? responseJson)
        {
            (CaseRepository repository, _) = Create(responseJson: responseJson);

            await Assert.ThrowsAsync<IcmResponseException>(() => repository.SearchAsync("t", Query));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task EveryReadThrowsOnARealFailure(HttpStatusCode statusCode)
        {
            (CaseRepository repository, _) = Create(statusCode, """{"message":"nope"}""");

            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(
                () => repository.SearchAsync("t", Query))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(
                () => repository.GetAsync("t", "1-5371KIQ"))).StatusCode);
            Assert.Equal(statusCode, (await Assert.ThrowsAsync<ApiException>(
                () => repository.GetContactsAsync("t", "1-5371KIQ"))).StatusCode);
        }

        [Fact]
        public async Task GetAsync_MapsTheRecord()
        {
            // A single read answers the record itself, not an items wrapper.
            (CaseRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """
                    {
                      "Id": "1-5371KIQ",
                      "Type": "Employment and Assistance",
                      "Office Name": "106 - Victoria Vefra",
                      "Link": [ { "rel": "self", "href": "https://icm/x", "name": "Case" } ]
                    }
                    """);

            Case? found = await repository.GetAsync("t", "1-5371KIQ", new CaseReadOptions { ViewMode = "Group" });

            Assert.Equal("106 - Victoria Vefra", found!.OfficeName);
            Assert.Empty(found.AdditionalFields);
            Assert.Contains("ViewMode=Group", handler.Request!.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task GetAsync_TurnsNoSuchCaseIntoNull(HttpStatusCode statusCode)
        {
            (CaseRepository repository, _) = Create(statusCode, null);

            Assert.Null(await repository.GetAsync("t", "1-NOPE"));
        }

        [Fact]
        public async Task GetContactsAsync_MapsTheRows()
        {
            // The live shape, MEASURED 2026-09-24, including the lastpage flag the child
            // collection adds and nothing models.
            (CaseRepository repository, _) = Create(
                responseJson: """
                    {
                      "lastpage": "true",
                      "items": [
                        { "Id": "1-532MU4J", "Relationship": "Key player", "Primary": "Y", "BCeID User Name": "winnona-afa-test" },
                        { "Id": "1-OTHER", "Relationship": "Spouse", "Primary": "N" }
                      ],
                      "Link": [ { "rel": "self", "href": "https://icm/x/Contact", "name": "Contact" } ]
                    }
                    """);

            IReadOnlyList<CaseContact> people = await repository.GetContactsAsync("t", "1-5371KIQ");

            Assert.Equal(["1-532MU4J", "1-OTHER"], people.Select(person => person.Id));
            Assert.Equal("Key player", people[0].Relationship);
            Assert.True(people[0].IsPrimary);
            Assert.Equal("winnona-afa-test", people[0].BceidUserName);
            Assert.False(people[1].IsPrimary);
            Assert.Empty(people[0].AdditionalFields);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task GetContactsAsync_TurnsNothingIntoAnEmptyList(HttpStatusCode statusCode)
        {
            (CaseRepository repository, _) = Create(statusCode, null);

            Assert.Empty(await repository.GetContactsAsync("t", "1-5371KIQ"));
        }

        [Fact]
        public async Task ARefusedCallNeverReachesIcm()
        {
            (CaseRepository repository, RecordingHttpMessageHandler handler) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SearchAsync("t", null!));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SearchAsync("t", new CaseQuery()));
            await Assert.ThrowsAsync<ArgumentException>(
                () => repository.SearchAsync("t", new CaseQuery { CaseNumber = "x\" OR [Status] LIKE \"*" }));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync("t", " "));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetContactsAsync("t", string.Empty));

            Assert.Null(handler.Request);
        }

        [Fact]
        public async Task SearchAsync_SendsTheTokenTheTrustedUserAndTheSearch()
        {
            (CaseRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "items": [] }""", trustedUserName: "IDIR\\SOMEONE");

            await repository.SearchAsync("token-abc", new CaseQuery { CaseNumber = "1-11077140770" });

            Assert.Equal("token-abc", handler.Request!.Headers.Authorization!.Parameter);
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request.Headers.GetValues(IServiceRequestApi.TrustedUserNameHeader)));
            Assert.Contains(
                "searchspec=[Case Num] = \"1-11077140770\"",
                Uri.UnescapeDataString(handler.Request.RequestUri!.AbsoluteUri),
                StringComparison.Ordinal);
        }
    }
}
