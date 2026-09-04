namespace Icm.Api.Tests.Repositories
{
    using System.Net;
    using System.Text.Json;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// The real Refit stack over canned responses, like the service request repository's
    /// tests. The one behaviour of its own worth pinning: a business rejection inside a
    /// 200 is returned, not thrown — and a 200 with no body is an error, because a
    /// submission has no empty success.
    /// </summary>
    public class BusPassRepositoryTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static readonly DateTimeOffset Now = new(2026, 9, 3, 17, 30, 5, TimeSpan.Zero);

        private static BusPassApplication Application() => new()
        {
            RequestType = BusPassRequestType.Replacement,
            LastName = "Example",
        };

        private static (BusPassRepository Repository, RecordingHttpMessageHandler Handler) Create(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = "{}",
            string? trustedUserName = null)
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (
                new BusPassRepository(httpClient, trustedUserName, new FakeTimeProvider(Now)),
                handler);
        }

        [Fact]
        public async Task SubmitAsync_MapsTheOutArgsIntoThePublishedResult()
        {
            (BusPassRepository repository, _) = Create(
                responseJson: """
                    {
                      "ApplicationNumber": "AP-12345",
                      "Error Code": "",
                      "Error Message": "",
                      "First Name": "Pat",
                      "Last Name": "Example",
                      "Status": "SUCCESS"
                    }
                    """);

            BusPassResult result = await repository.SubmitAsync("t", Application());

            Assert.Equal("AP-12345", result.ApplicationNumber);
            Assert.Equal("Pat", result.FirstName);
            Assert.Equal("SUCCESS", result.Status);
        }

        [Fact]
        public async Task SubmitAsync_ReturnsABusinessRejectionRatherThanThrowing()
        {
            // The workflow's error vocabulary is undocumented, so the repository hands the
            // rejection to the caller instead of guessing which words are fatal.
            (BusPassRepository repository, _) = Create(
                responseJson: """
                    { "Error Code": "SBL-EXL-00151", "Error Message": "No match", "Status": "ERROR" }
                    """);

            BusPassResult result = await repository.SubmitAsync("t", Application());

            Assert.Equal("SBL-EXL-00151", result.ErrorCode);
            Assert.Equal("No match", result.ErrorMessage);
        }

        [Fact]
        public async Task SubmitAsync_SendsTheMappedEnvelopeWithTheStampedClock()
        {
            (BusPassRepository repository, RecordingHttpMessageHandler handler) = Create();

            await repository.SubmitAsync("t", Application());

            using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
            JsonElement inbound = body.RootElement
                .GetProperty("SRInboundMessage")
                .GetProperty("ListOfICMSRBusPassInboundIO")
                .GetProperty("ICMSRInbound")[0];
            Assert.Equal(
                "20260903T173005Z",
                inbound.GetProperty("ListOfHeader").GetProperty("Header")[0]
                    .GetProperty("Timestamp").GetString());
            Assert.Equal(
                "Example",
                inbound.GetProperty("ListOfPayload").GetProperty("Payload")[0]
                    .GetProperty("ListOfSRProspects").GetProperty("SRProspects")[0]
                    .GetProperty("LstNme").GetString());
        }

        [Fact]
        public async Task SubmitAsync_TreatsSuccessWithNoBodyAsAnIcmResponseProblem()
        {
            (BusPassRepository repository, _) = Create(responseJson: null);

            await Assert.ThrowsAsync<IcmResponseException>(
                () => repository.SubmitAsync("t", Application()));
        }

        [Fact]
        public async Task SubmitAsync_LetsARealFailureSurfaceAsAnApiException()
        {
            (BusPassRepository repository, _) = Create(HttpStatusCode.InternalServerError);

            await Assert.ThrowsAsync<ApiException>(
                () => repository.SubmitAsync("t", Application()));
        }

        [Fact]
        public async Task SubmitAsync_RefusesANullApplication()
        {
            (BusPassRepository repository, _) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => repository.SubmitAsync("t", null!));
        }
    }
}
