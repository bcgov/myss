namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.Api.Configuration;
    using Myss.Api.Data;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// The attachments endpoint through the real pipeline: multipart binding,
    /// the scan flow, keyworded rejections and owner scoping. The scanner,
    /// the store and the database are swapped for in-memory doubles.
    /// </summary>
    public class AttachmentsEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeVirusScanProvider _scanner = new();
        private readonly InMemoryFileStorageProvider _storage = new();

        /// <summary>Initializes a new instance of the <see cref="AttachmentsEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public AttachmentsEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task UnauthenticatedUploadIsRejectedWith401()
        {
            using HttpClient client = CreateClient(mockAuth: false);

            using HttpResponseMessage response = await client.PostAsync("/v1/attachments", PdfForm("f.pdf"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CleanUploadIsReleasedStoredAndListedForItsOwnerOnly()
        {
            using HttpClient client = CreateClient();

            using HttpResponseMessage response = await client.SendAsync(
                Post("/v1/attachments", "alice", PdfForm("statement.pdf")));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement payload = body.RootElement.GetProperty("payload");
            Assert.Equal("statement.pdf", payload.GetProperty("fileName").GetString());
            Assert.Equal("Released", payload.GetProperty("status").GetString());
            Assert.Single(_storage.Objects);
            Assert.Single(_scanner.ScannedPayloads);

            // The owner sees it; another persona does not.
            Assert.Single(await ListAttachments(client, "alice"));
            Assert.Empty(await ListAttachments(client, "bob"));
        }

        [Fact]
        public async Task InfectedUploadIsRejectedWith422KeywordAndNotStoredOrListed()
        {
            using HttpClient client = CreateClient();
            _scanner.Result = new VirusScanResult(IsClean: false, "Win.Test.EICAR_HDB-1");

            using HttpResponseMessage response = await client.SendAsync(
                Post("/v1/attachments", "alice", PdfForm("eicar.pdf")));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("DOC.UPLOAD.INFECTED", await ReadKeyword(response));
            Assert.Empty(_storage.Objects);

            // The rejected audit row should not show up in the owner's list.
            Assert.Empty(await ListAttachments(client, "alice"));
        }

        [Fact]
        public async Task ScannerOutageYields503WithKeywordAndNothingIsStored()
        {
            using HttpClient client = CreateClient();
            _scanner.Unavailable = true;

            using HttpResponseMessage response = await client.SendAsync(
                Post("/v1/attachments", "alice", PdfForm("statement.pdf")));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("DOC.SCAN.UNAVAILABLE", await ReadKeyword(response));
            Assert.Empty(_storage.Objects);
        }

        [Fact]
        public async Task DisallowedContentTypeYields400WithKeyword()
        {
            using HttpClient client = CreateClient();

            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent([1, 2, 3, 4]);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
            form.Add(file, "file", "run.exe");

            using HttpResponseMessage response = await client.SendAsync(Post("/v1/attachments", "alice", form));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("DOC.UPLOAD.TYPE_NOT_ALLOWED", await ReadKeyword(response));
            Assert.Empty(_scanner.ScannedPayloads);
        }

        [Fact]
        public async Task MismatchedContentYields400WithKeyword()
        {
            // Declared as PDF, junk bytes: the magic-byte check rejects it.
            using HttpClient client = CreateClient();

            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent([1, 2, 3, 4]);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", "not-really.pdf");

            using HttpResponseMessage response = await client.SendAsync(Post("/v1/attachments", "alice", form));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("DOC.UPLOAD.TYPE_NOT_ALLOWED", await ReadKeyword(response));
        }

        private HttpClient CreateClient(bool mockAuth = true)
        {
            string dbName = Guid.NewGuid().ToString();
            return _factory
                .WithWebHostBuilder(builder =>
                {
                    string enabled = mockAuth ? "true" : "false";
                    builder.UseMockAuthSettings(
                        allowMockAuth: enabled, environmentName: "test", mockAuth: enabled);

                    builder.ConfigureServices(services =>
                    {
                        // The InMemory provider needs its own internal EF
                        // service provider — the forms context still registers
                        // Npgsql, and EF only allows one database provider per
                        // internal service provider.
                        ServiceProvider efProvider = new ServiceCollection()
                            .AddEntityFrameworkInMemoryDatabase()
                            .BuildServiceProvider();
                        services.RemoveAll<DbContextOptions<AttachmentsDbContext>>();
                        services.AddDbContext<AttachmentsDbContext>(
                            options => options
                                .UseInMemoryDatabase(dbName)
                                .UseInternalServiceProvider(efProvider));
                        services.RemoveAll<IVirusScanProvider>();
                        services.AddSingleton<IVirusScanProvider>(_scanner);
                        services.RemoveAll<IFileStorageProvider>();
                        services.AddSingleton<IFileStorageProvider>(_storage);
                    });
                })
                .CreateClient();
        }

        private static MultipartFormDataContent PdfForm(string fileName)
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent("%PDF-1.7 test"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", fileName);
            return form;
        }

        private static HttpRequestMessage Post(string path, string persona, HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
            request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            return request;
        }

        private static async Task<string?> ReadKeyword(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("keyword").GetString();
        }

        private static async Task<JsonElement.ArrayEnumerator> ListAttachments(HttpClient client, string persona)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/v1/attachments");
            request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            using HttpResponseMessage response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").EnumerateArray();
        }
    }
}
