namespace Icm.Api.Tests.Repositories
{
    using System.Net;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// The contact repository over the real Refit stack and canned responses: what ICM's
    /// status codes become for a caller.
    /// </summary>
    public class ContactRepositoryTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static ContactQuery Query => new() { BcServicesCardDid = "MYSS+synthetic/DID0123456789abcdefghijklmno=" };

        private static (ContactRepository Repository, RecordingHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = "{}",
            string? trustedUserName = null)
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (new ContactRepository(httpClient, trustedUserName), handler);
        }

        [Fact]
        public async Task SearchAsync_MapsTheMatchIntoAPublishedModel()
        {
            // The live shape, MEASURED 2026-09-17: items is an array even for one match
            // (uniformresponse=Y), and each record carries self/canonical links.
            (ContactRepository repository, _) = Create(
                responseJson: """
                    {
                      "items": [
                        {
                          "Id": "1-TEST01",
                          "First Name": "Myss",
                          "Last Name": "IntegrationTest",
                          "Birth Date": "01/31/1950",
                          "Primary Email": "",
                          "Deceased Flag": "N",
                          "Link": [ { "rel": "self", "href": "https://icm/x", "name": "ICMContact" } ]
                        }
                      ],
                      "Link": [ { "rel": "self", "href": "https://icm/list", "name": "ICMContact" } ]
                    }
                    """);

            Contact contact = Assert.Single((await repository.SearchAsync("t", Query)).Items);

            Assert.Equal("1-TEST01", contact.Id);
            Assert.Equal("IntegrationTest", contact.LastName);
            Assert.Equal(new DateOnly(1950, 1, 31), contact.BirthDate);
            Assert.False(contact.IsDeceased);
            Assert.Empty(contact.AdditionalFields);
        }

        [Fact]
        public async Task SearchAsync_ReturnsEveryMatch_BecauseChoosingOneIsNotItsCall()
        {
            (ContactRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "items": [ { "Id": "1-TEST01" }, { "Id": "1-TEST02" } ] }""");
            handler.ResponseHeaders["Total-Record-Count"] = "12";

            ContactPage page = await repository.SearchAsync("t", Query);

            Assert.Equal(["1-TEST01", "1-TEST02"], page.Items.Select(contact => contact.Id));
            Assert.Equal(12, page.TotalCount);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task SearchAsync_TurnsNoSuchContactIntoAnEmptyPage(HttpStatusCode statusCode)
        {
            // The 404 is what SIT1 sends (MEASURED 2026-09-17); the 204 is documented.
            (ContactRepository repository, _) = Create(
                statusCode,
                statusCode == HttpStatusCode.NotFound
                    ? """{"ERROR":"There is no data for the requested resource"}"""
                    : null);

            ContactPage page = await repository.SearchAsync("t", Query);

            Assert.Empty(page.Items);
            Assert.Null(page.TotalCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{}")]
        public async Task SearchAsync_ThrowsWhenIcmClaimsSuccessButReturnsNoRecords(string? responseJson)
        {
            // Not "not found": that would tell a citizen they have no file because ICM
            // had a bad moment.
            (ContactRepository repository, _) = Create(responseJson: responseJson);

            await Assert.ThrowsAsync<IcmResponseException>(() => repository.SearchAsync("t", Query));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task SearchAsync_ThrowsOnARealFailure(HttpStatusCode statusCode)
        {
            (ContactRepository repository, _) = Create(statusCode, """{"message":"nope"}""");

            ApiException exception = await Assert.ThrowsAsync<ApiException>(
                () => repository.SearchAsync("t", Query));

            Assert.Equal(statusCode, exception.StatusCode);
        }

        [Fact]
        public async Task SearchAsync_SendsTheTokenTheTrustedUserAndTheSearch()
        {
            (ContactRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "items": [] }""", trustedUserName: "IDIR\\SOMEONE");

            await repository.SearchAsync(
                "token-abc",
                new ContactQuery { Sin = "046454286", LastName = "IntegrationTest", ViewMode = "Organization" });

            Assert.Equal("token-abc", handler.Request!.Headers.Authorization!.Parameter);
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request.Headers.GetValues(IServiceRequestApi.TrustedUserNameHeader)));

            string query = Uri.UnescapeDataString(handler.Request.RequestUri!.AbsoluteUri);
            Assert.Contains(
                "searchspec=[SIN] = \"046454286\" AND [Last Name] ~= \"IntegrationTest\"",
                query,
                StringComparison.Ordinal);
            Assert.Contains("ViewMode=Organization", query, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ARefusedQueryNeverReachesIcm()
        {
            (ContactRepository repository, RecordingHttpMessageHandler handler) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SearchAsync("t", null!));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SearchAsync("t", new ContactQuery()));
            await Assert.ThrowsAsync<ArgumentException>(
                () => repository.SearchAsync("t", new ContactQuery { LastName = "x\" OR [SIN] LIKE \"*" }));

            Assert.Null(handler.Request);
        }
    }
}
