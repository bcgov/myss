namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.Api.Configuration;
    using Myss.Api.Intake;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;
    using Myss.Data.Intake;

    /// <summary>
    /// Supporting documents on an application: added and removed while it is
    /// a draft, kept and read-only once it is submitted, private to its owner.
    /// </summary>
    public class ApplicationAttachmentsEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/applications";

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        public ApplicationAttachmentsEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 1, Spec);
            _provider.VersionResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 1, Spec);
        }

        [Fact]
        public async Task TheOwner_CanAddAndListDocumentsOnADraft()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");

            using HttpResponseMessage upload = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/attachments", Pdf("paystub.pdf"));

            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            JsonElement added = await Payload(upload);
            Assert.Equal("paystub.pdf", added.GetProperty("fileName").GetString());
            Assert.Equal(id, added.GetProperty("submissionId").GetGuid());
            Assert.Single(host.Storage.Objects);

            JsonElement[] listed = await List(host, "alice", id);
            Assert.Equal(added.GetProperty("id").GetGuid(), Assert.Single(listed).GetProperty("id").GetGuid());
        }

        [Fact]
        public async Task TheOwner_CanRemoveADocumentFromADraft()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            Guid attachmentId = await Upload(host, "alice", id);

            using HttpResponseMessage delete = await host.Send("alice", HttpMethod.Delete, $"{Route}/{id}/attachments/{attachmentId}");

            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            Assert.Empty(await List(host, "alice", id));
            Assert.Empty(host.Storage.Objects);
        }

        [Fact]
        public async Task OnceSubmitted_DocumentsStayButCannotBeChanged()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            Guid attachmentId = await Upload(host, "alice", id);
            using HttpResponseMessage submit = await host.Send(
                "alice", HttpMethod.Post, $"{Route}/{id}/submit", JsonContent.Create(new { version = 1, answers = new { firstName = "Ada" } }));
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

            using HttpResponseMessage add = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/attachments", Pdf("late.pdf"));
            using HttpResponseMessage delete = await host.Send("alice", HttpMethod.Delete, $"{Route}/{id}/attachments/{attachmentId}");

            Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(add));
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
            Assert.Single(await List(host, "alice", id));
        }

        [Fact]
        public async Task AnotherCitizen_CannotSeeOrChangeTheDocuments()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            Guid attachmentId = await Upload(host, "alice", id);

            using HttpResponseMessage list = await host.Send("bob", HttpMethod.Get, $"{Route}/{id}/attachments");
            using HttpResponseMessage add = await host.Send("bob", HttpMethod.Post, $"{Route}/{id}/attachments", Pdf("x.pdf"));
            using HttpResponseMessage delete = await host.Send("bob", HttpMethod.Delete, $"{Route}/{id}/attachments/{attachmentId}");

            Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            Assert.Single(await List(host, "alice", id));
        }

        [Fact]
        public async Task AnInfectedFile_IsRefusedAndNotListed()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            host.Scanner.Result = new VirusScanResult(IsClean: false, Signature: "Eicar-Test-Signature");

            using HttpResponseMessage upload = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/attachments", Pdf("bad.pdf"));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, upload.StatusCode);
            Assert.Equal("DOC.UPLOAD.INFECTED", await Keyword(upload));
            Assert.Empty(await List(host, "alice", id));
            Assert.Empty(host.Storage.Objects);
        }

        [Fact]
        public async Task ADisallowedType_IsRefused()
        {
            using IntakeTestHost host = NewHost();
            Guid id = await host.Create("alice");
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent("hello"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(file, "file", "notes.txt");

            using HttpResponseMessage upload = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/attachments", form);

            Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
            Assert.Equal("DOC.UPLOAD.TYPE_NOT_ALLOWED", await Keyword(upload));
        }

        private static MultipartFormDataContent Pdf(string fileName)
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent("%PDF-1.7 test"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", fileName);
            return form;
        }

        private static async Task<Guid> Upload(IntakeTestHost host, string persona, Guid id)
        {
            using HttpResponseMessage upload = await host.Send(persona, HttpMethod.Post, $"{Route}/{id}/attachments", Pdf("doc.pdf"));
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            return (await Payload(upload)).GetProperty("id").GetGuid();
        }

        private static async Task<JsonElement[]> List(IntakeTestHost host, string persona, Guid id)
        {
            using HttpResponseMessage list = await host.Send(persona, HttpMethod.Get, $"{Route}/{id}/attachments");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            return (await Payload(list)).EnumerateArray().ToArray();
        }

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").Clone();
        }

        private static async Task<string?> Keyword(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("keyword").GetString();
        }

        private IntakeTestHost NewHost() => new(_factory, _provider, registered: ["alice", "bob"], mockAuth: true);
    }
}
