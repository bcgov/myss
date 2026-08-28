namespace Icm.Api.Tests.Repositories
{
    using System.Net;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// The repository's job is turning ICM's status codes into terms a caller can use.
    /// These run the real Refit stack over a canned response, so what is being checked is
    /// the actual translation and not a stubbed idea of it.
    /// </summary>
    public class ServiceRequestRepositoryTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static (ServiceRequestRepository Repository, RecordingHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = "{}",
            string? trustedUserName = null)
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (new ServiceRequestRepository(httpClient, trustedUserName), handler);
        }

        [Fact]
        public async Task SearchAsync_MapsThePageIntoPublishedModels()
        {
            (ServiceRequestRepository repository, _) = Create(
                responseJson: """
                    {
                      "items": [
                        { "Service Request Number": "1-1", "Restricted Flag": "Y" },
                        { "Service Request Number": "1-2", "Restricted Flag": "N" }
                      ],
                      "Link": [ { "rel": "next", "href": "https://icm/next" } ]
                    }
                    """);

            ServiceRequestPage page = await repository.SearchAsync("t");

            Assert.Equal(2, page.Items.Count);
            Assert.Equal("1-1", page.Items[0].ServiceRequestNumber);
            Assert.True(page.Items[0].RestrictedFlag);
            Assert.False(page.Items[1].RestrictedFlag);
            Assert.Equal("next", Assert.Single(page.Links).Rel);
        }

        [Fact]
        public async Task SearchAsync_TurnsAnEmptyResultIntoAnEmptyPage()
        {
            // ICM says 204 for "nothing matched". A caller should not have to know that.
            (ServiceRequestRepository repository, _) = Create(HttpStatusCode.NoContent, null);

            ServiceRequestPage page = await repository.SearchAsync("t");

            Assert.Empty(page.Items);
        }

        [Fact]
        public async Task SearchAsync_ThrowsOnARealFailure()
        {
            (ServiceRequestRepository repository, _) = Create(HttpStatusCode.Unauthorized, null);

            await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync("t"));
        }

        [Fact]
        public async Task SearchAsync_AppliesTheQueryItWasGiven()
        {
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) = Create();

            await repository.SearchAsync("t", new ServiceRequestQuery
            {
                SearchSpec = "[Status] = \"Open\"",
                Fields = ["Service Request Number"],
                PageSize = 10,
            });

            string query = Uri.UnescapeDataString(handler.Request!.RequestUri!.Query);
            Assert.Contains("uniformresponse=Y", query, StringComparison.Ordinal);
            Assert.Contains("searchspec=[Status] = \"Open\"", query, StringComparison.Ordinal);
            Assert.Contains("fields=Service Request Number", query, StringComparison.Ordinal);
            Assert.Contains("PageSize=10", query, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task GetAsync_ReportsAMissingRecordAsNull(HttpStatusCode statusCode)
        {
            // The spec uses both codes for "there is no such record", and ICM does not say
            // which of "absent" and "not yours" it means.
            (ServiceRequestRepository repository, _) = Create(statusCode, null);

            Assert.Null(await repository.GetAsync("t", "1-NOPE"));
        }

        [Fact]
        public async Task GetAsync_MapsTheRecord()
        {
            (ServiceRequestRepository repository, _) = Create(
                responseJson: """{ "Service Request Number": "1-12345", "Cell Phone": "250-555-0100" }""");

            ServiceRequest? sr = await repository.GetAsync("t", "1-ABCDE");

            Assert.Equal("1-12345", sr!.ServiceRequestNumber);
            Assert.Equal("250-555-0100", sr.CellPhone);
        }

        [Fact]
        public async Task CreateAsync_SendsOnlyTheFieldsThatWereSetAndReturnsTheRecord()
        {
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "items": { "Id": "1-NEW", "Service Request Number": "1-99" } }""");

            ServiceRequest created = await repository.CreateAsync(
                "t",
                new ServiceRequestInput { Type = "Application", RestrictedFlag = true });

            Assert.Equal(
                """{"Restricted Flag":"Y","Type":"Application"}""",
                handler.RequestBody);
            Assert.Equal("1-NEW", created.Id);
            Assert.Equal("1-99", created.ServiceRequestNumber);
        }

        [Fact]
        public async Task GetAsync_DeliversTypedValuesThroughTheWholeStack()
        {
            (ServiceRequestRepository repository, _) = Create(
                responseJson: """
                    {
                      "Service Request Number": "1-12345",
                      "Restricted Flag": "Y",
                      "Created Date": "08/27/2026 10:15:00",
                      "Call Date": "08/27/2026 14:30:00",
                      "ICM CGA Resolution Decision Date": "09/01/2026"
                    }
                    """);

            ServiceRequest? sr = await repository.GetAsync("t", "1-ABCDE");

            Assert.True(sr!.RestrictedFlag);
            Assert.Equal(new DateTime(2026, 8, 27, 10, 15, 0), sr.CreatedDate);
            Assert.Equal(new DateTime(2026, 8, 27, 14, 30, 0), sr.CallDate);
            Assert.Equal(new DateOnly(2026, 9, 1), sr.ICMCGAResolutionDecisionDate);
            Assert.Empty(sr.UnparsedValues);
        }

        [Fact]
        public async Task CreateAsync_FormatsTypedValuesBackIntoSiebelsShape()
        {
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "items": { "Id": "1-NEW" } }""");

            await repository.CreateAsync(
                "t",
                new ServiceRequestInput
                {
                    Type = "Application",
                    RestrictedFlag = false,
                    CallDate = new DateTime(2026, 8, 27, 14, 30, 0),
                    ICMCGAResolutionDecisionDate = new DateOnly(2026, 9, 1),
                });

            Assert.Equal(
                """{"Call Date":"08/27/2026 14:30:00","ICM CGA Resolution Decision Date":"09/01/2026","Restricted Flag":"N","Type":"Application"}""",
                handler.RequestBody);
        }

        [Fact]
        public async Task CreateAsync_ThrowsWhenIcmClaimsSuccessButReturnsNoRecord()
        {
            // A create has no "nothing changed" outcome, so an empty body means the two
            // ends disagree about what just happened.
            (ServiceRequestRepository repository, _) = Create(responseJson: "{}");

            await Assert.ThrowsAsync<IcmResponseException>(
                () => repository.CreateAsync("t", new ServiceRequestInput()));
        }

        [Fact]
        public async Task UpdateAsync_ReportsNotModifiedAsNull()
        {
            (ServiceRequestRepository repository, _) = Create(HttpStatusCode.NotModified, null);

            Assert.Null(await repository.UpdateAsync("t", "1-ABCDE", new ServiceRequestInput()));
        }

        [Fact]
        public async Task UpdateAsync_ReturnsTheStoredRecord()
        {
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) = Create(
                responseJson: """{ "items": { "Id": "1-ABCDE", "Status": "Closed" } }""");

            ServiceRequest? updated = await repository.UpdateAsync(
                "t", "1-ABCDE", new ServiceRequestInput { Status = "Closed" });

            Assert.Equal(HttpMethod.Put, handler.Request!.Method);
            Assert.Equal(
                "/gov/v1.0/data/ServiceRequest/ServiceRequest/1-ABCDE/",
                handler.Request.RequestUri!.AbsolutePath);
            Assert.Equal("Closed", updated!.Status);
        }

        [Fact]
        public async Task DeleteAsync_ReportsWhetherThereWasAnythingToDelete()
        {
            (ServiceRequestRepository deleted, _) = Create();
            Assert.True(await deleted.DeleteAsync("t", "1-ABCDE"));

            (ServiceRequestRepository missing, _) = Create(HttpStatusCode.NotFound, null);
            Assert.False(await missing.DeleteAsync("t", "1-NOPE"));
        }

        [Fact]
        public async Task EveryCallCarriesTheTrustedUserNameItWasBuiltWith()
        {
            // Configured once on the repository rather than passed per call, because it
            // identifies this application's ICM service account.
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) =
                Create(trustedUserName: "IDIR\\SOMEONE");

            await repository.SearchAsync("t");
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request!.Headers.GetValues("X-ICM-TrustedUserName")));

            await repository.DeleteAsync("t", "1-ABCDE");
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request!.Headers.GetValues("X-ICM-TrustedUserName")));
        }

        [Fact]
        public async Task EveryCallCarriesTheTokenItWasGiven()
        {
            (ServiceRequestRepository repository, RecordingHttpMessageHandler handler) = Create();

            await repository.SearchAsync("token-abc");

            Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
            Assert.Equal("token-abc", handler.Request.Headers.Authorization.Parameter);
        }
    }
}
