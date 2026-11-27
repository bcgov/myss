extern alias citizen;

namespace Myss.AdminApi.Tests.Intake
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.AdminApi.Tests.TestSupport;

    /// <summary>
    /// Supporting documents from the worker side: a citizen adds files to an
    /// application in MyssApi, and once it is submitted a worker lists and
    /// downloads them here. Drafts stay private, and citizens cannot reach
    /// these routes.
    /// </summary>
    public class ReviewAttachmentsEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/review/applications";

        private const string IntakeFormSpecId = citizen::Myss.Api.Intake.IntakeService.FormSpecId;

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private static readonly byte[] PdfBytes = "%PDF-1.7 paystub"u8.ToArray();

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="ReviewAttachmentsEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public ReviewAttachmentsEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeFormSpecId, 1, Spec);
            _provider.VersionResult = FakeFormSpecProvider.Spec(IntakeFormSpecId, 1, Spec);
        }

        [Fact]
        public async Task AWorker_SeesAndDownloadsTheDocumentsOfASubmittedApplication()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await host.Create("alice");
            Guid attachmentId = await Upload(host, "alice", id, "paystub.pdf");
            await host.Submit("alice", id, 1, new { firstName = "Ada" });

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, $"{Route}/{id}/attachments");

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            JsonElement row = Assert.Single((await Payload(list)).EnumerateArray().ToList());
            Assert.Equal(attachmentId, row.GetProperty("id").GetGuid());
            Assert.Equal("paystub.pdf", row.GetProperty("fileName").GetString());

            using HttpResponseMessage download = await host.Send(
                "worker", HttpMethod.Get, $"{Route}/{id}/attachments/{attachmentId}/content");

            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(PdfBytes, await download.Content.ReadAsByteArrayAsync());
        }

        [Fact]
        public async Task ADownload_CanRunNothingAndIsNotCached()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await host.Create("alice");
            Guid attachmentId = await Upload(host, "alice", id, "paystub.pdf");
            await host.Submit("alice", id, 1, new { firstName = "Ada" });

            using HttpResponseMessage download = await host.Send(
                "worker", HttpMethod.Get, $"{Route}/{id}/attachments/{attachmentId}/content");

            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal(
                "default-src 'none'; sandbox",
                Assert.Single(download.Headers.GetValues("Content-Security-Policy")));
            Assert.True(download.Headers.CacheControl?.Private);
            Assert.True(download.Headers.CacheControl?.NoStore);
            Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        }

        [Fact]
        public async Task OnlyTheDocumentsTheSubmitRecorded_AreShown()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await host.Create("alice");
            Guid recorded = await Upload(host, "alice", id, "paystub.pdf");
            await host.Submit("alice", id, 1, new { firstName = "Ada" });
            Guid late = await host.AddReleasedAttachment(id, "alice");

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, $"{Route}/{id}/attachments");
            using HttpResponseMessage download = await host.Send(
                "worker", HttpMethod.Get, $"{Route}/{id}/attachments/{late}/content");

            Guid shown = Assert.Single((await Payload(list)).EnumerateArray().ToList()).GetProperty("id").GetGuid();
            Assert.Equal(recorded, shown);
            Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        }

        [Fact]
        public async Task ADraftsDocuments_AreNotVisibleToWorkers()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await host.Create("alice");
            Guid attachmentId = await Upload(host, "alice", id, "paystub.pdf");

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, $"{Route}/{id}/attachments");
            using HttpResponseMessage download = await host.Send(
                "worker", HttpMethod.Get, $"{Route}/{id}/attachments/{attachmentId}/content");

            Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        }

        [Fact]
        public async Task ADocument_CannotBeOpenedThroughAnotherApplication()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid alices = await host.Create("alice");
            Guid attachmentId = await Upload(host, "alice", alices, "paystub.pdf");
            await host.Submit("alice", alices, 1, new { firstName = "Ada" });
            Guid bobs = await host.Create("bob");
            await host.Submit("bob", bobs, 1, new { firstName = "Bob" });

            using HttpResponseMessage download = await host.Send(
                "worker", HttpMethod.Get, $"{Route}/{bobs}/attachments/{attachmentId}/content");

            Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        }

        [Fact]
        public async Task ACitizen_CannotUseTheWorkerRoutes()
        {
            using CitizenAndStaffTestHost host = NewHost();
            Guid id = await host.Create("alice");
            await Upload(host, "alice", id, "paystub.pdf");
            await host.Submit("alice", id, 1, new { firstName = "Ada" });

            using HttpResponseMessage list = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}/attachments");

            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        }

        private static async Task<Guid> Upload(CitizenAndStaffTestHost host, string persona, Guid id, string fileName)
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(PdfBytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", fileName);
            using HttpResponseMessage upload = await host.Send(
                persona, HttpMethod.Post, $"/v1/intake/applications/{id}/attachments", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            return (await Payload(upload)).GetProperty("id").GetGuid();
        }

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").Clone();
        }

        private CitizenAndStaffTestHost NewHost() =>
            new(_factory, _provider, ["alice", "bob"], mockAuth: true);
    }
}
