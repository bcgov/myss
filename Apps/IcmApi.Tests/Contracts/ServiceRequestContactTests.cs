namespace Icm.Api.Tests.Contracts
{
    using System.Net;
    using System.Text.Json;
    using Icm.Api;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// Linking a contact to a service request, and reading the contacts on one — the
    /// shape MEASURED on SIT2 on 2026-09-29 (ICM's "PUT Contact to Service Request"
    /// guidance, ADO 8628, in the child view's field names).
    /// </summary>
    public class ServiceRequestContactTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static (ServiceRequestRepository Repository, RecordingHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK, string? responseJson = "{}")
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (new ServiceRequestRepository(httpClient), handler);
        }

        [Fact]
        public void ToSiebel_WritesTheLinkInTheChildViewsNames_WithTheWorkflowsDefaults()
        {
            SiebelServiceRequestContact row = ServiceRequestMapper.ToSiebel(new ServiceRequestContactLink { ContactId = "1-4118DDQ" });

            Assert.Equal("1-4118DDQ", row.Id);
            Assert.Equal("Key player", row.SRRelationship);
            Assert.Equal("Y", row.SSAPrimaryField);
            Assert.Null(row.SRSubjectFlag);

            // The guidance's "Relationship"/"Subject" are not what ICM honours; the child
            // view's names are, and only those travel.
            string json = JsonSerializer.Serialize(row, IcmRefitSettings.JsonOptions);
            Assert.Equal("""{"Id":"1-4118DDQ","SR Relationship":"Key player","SSA Primary Field":"Y"}""", json);

            SiebelServiceRequestContact spouse = ServiceRequestMapper.ToSiebel(
                new ServiceRequestContactLink { ContactId = "1-532MU4J", Relationship = "Spouse", IsPrimary = false, IsSubject = false });
            Assert.Equal("Spouse", spouse.SRRelationship);
            Assert.Equal("N", spouse.SSAPrimaryField);
            Assert.Equal("N", spouse.SRSubjectFlag);

            Assert.Throws<ArgumentNullException>(() => ServiceRequestMapper.ToSiebel((ServiceRequestContactLink)null!));
            Assert.Throws<ArgumentException>(() => ServiceRequestMapper.ToSiebel(new ServiceRequestContactLink { ContactId = " " }));
        }

        [Fact]
        public void ToModel_TypesALinkRow()
        {
            // The live row on the workflow's matched SR 1-53CJXK6.
            ServiceRequestContact model = ServiceRequestMapper.ToModel(new SiebelServiceRequestContact
            {
                Id = "1-4118DDQ", SRId = "1-53CJXK6", SRRelationship = "Key player", SSAPrimaryField = "Y", SRSubjectFlag = "N",
                SRStart = "09/14/2026 00:00:00", FirstName = "Fillip", LastName = "ERIQAP-test-CH", BirthDate = "01/01/1990",
                MF = "Man/Boy", Age = "36", AboriginalCalc = "TBD", Created = "09/14/2015 11:28:37", ICMCreatedBy = "HGWRK59",
            });

            Assert.Equal("1-4118DDQ", model.Id);
            Assert.Equal("1-53CJXK6", model.ServiceRequestId);
            Assert.Equal("Key player", model.Relationship);
            Assert.True(model.IsPrimary);
            Assert.False(model.IsSubject);
            Assert.Equal(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Unspecified), model.Start);
            Assert.Equal(new DateOnly(1990, 1, 1), model.BirthDate);
            Assert.Equal("HGWRK59", model.ContactCreatedBy);
            Assert.Empty(model.UnparsedValues);
            Assert.Empty(ServiceRequestMapper.ToModel((SiebelServiceRequestContactListResponse?)null));
        }

        [Fact]
        public async Task LinkContactAsync_PutsOnTheChildPath_AndReturnsTheServiceRequestsId()
        {
            // The live answer, MEASURED 2026-09-29: the SR with the link nested under it.
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) = Create(responseJson: """
                { "items": { "Id": "1-53MU3XD", "ICMContact": { "Id": "1-4118DDQ", "Link": { "rel": "self", "href": "https://icm/x", "name": "ICMContact" } },
                             "Link": { "rel": "self", "href": "https://icm/sr", "name": "ServiceRequest" } } }
                """);

            string id = await repository.LinkContactAsync("t", "1-53MU3XD", new ServiceRequestContactLink { ContactId = "1-4118DDQ" });

            Assert.Equal("1-53MU3XD", id);
            Assert.Equal(HttpMethod.Put, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/ServiceRequest/ServiceRequest/1-53MU3XD/ICMContact/", handler.Request.RequestUri!.AbsolutePath);
            Assert.Equal("""{"Id":"1-4118DDQ","SR Relationship":"Key player","SSA Primary Field":"Y"}""", handler.RequestBody);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{}")]
        [InlineData("""{ "items": { "ICMContact": { "Id": "1-4118DDQ" } } }""")]
        public async Task ALinkThatClaimsSuccessWithoutTheServiceRequestIsAnError(string? responseJson)
        {
            (ServiceRequestRepository repository, _) = Create(responseJson: responseJson);

            await Assert.ThrowsAsync<IcmResponseException>(
                () => repository.LinkContactAsync("t", "1-53MU3XD", new ServiceRequestContactLink { ContactId = "1-4118DDQ" }));
        }

        [Fact]
        public async Task GetContactsAsync_ReadsTheChild_AndFollowsPages()
        {
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) = Create();
            handler.NextResponses.Enqueue((HttpStatusCode.OK, """{ "lastpage": "false", "items": [ { "Id": "1-4118DDQ", "SR Relationship": "Key player", "SSA Primary Field": "Y" } ] }"""));
            handler.NextResponses.Enqueue((HttpStatusCode.OK, """{ "lastpage": "true", "items": [ { "Id": "1-532MU4J", "SR Relationship": "Spouse", "SSA Primary Field": "N" } ] }"""));

            IReadOnlyList<ServiceRequestContact> people = await repository.GetContactsAsync(
                "t", "1-53MU3XD", new ServiceRequestReadOptions { ViewMode = "Organization" });

            Assert.Equal(["1-4118DDQ", "1-532MU4J"], people.Select(p => p.Id));
            Assert.True(people[0].IsPrimary);
            Assert.Equal("Spouse", people[1].Relationship);
            Assert.Equal(2, handler.Requests.Count);
            Assert.All(handler.Requests, request => Assert.Equal(
                "/gov/v1.0/data/ServiceRequest/ServiceRequest/1-53MU3XD/ICMContact/", request.RequestUri!.AbsolutePath));
            Assert.Contains("ViewMode=Organization", handler.Requests[0].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task GetContactsAsync_TurnsNothingIntoAnEmptyList(HttpStatusCode statusCode)
        {
            (ServiceRequestRepository repository, _) = Create(statusCode, null);
            Assert.Empty(await repository.GetContactsAsync("t", "1-53MU3X7"));
        }

        [Fact]
        public async Task ARefusedCallNeverReachesIcm()
        {
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetContactsAsync("t", " "));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.LinkContactAsync("t", " ", new ServiceRequestContactLink { ContactId = "1-X" }));
            await Assert.ThrowsAsync<ArgumentNullException>(() => repository.LinkContactAsync("t", "1-53MU3XD", null!));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.LinkContactAsync("t", "1-53MU3XD", new ServiceRequestContactLink()));

            Assert.Null(handler.Request);
        }

        [Fact]
        public void TheWriteContractCarriesNoNestedContacts()
        {
            // Nesting contacts on the SR create is "Invalid input" in both the bare-array
            // and the ListOf shape (MEASURED 2026-09-29); linking is a second call.
            Assert.DoesNotContain(
                typeof(SiebelServiceRequestWrite).GetProperties(),
                property => property.Name.Contains("Contact", StringComparison.Ordinal) && property.PropertyType != typeof(string));
        }
    }
}
