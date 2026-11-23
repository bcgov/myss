namespace Myss.Api.Tests
{
    using Myss.Common.Storage;
    using Myss.Data.Attachments;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.Api.Configuration;
    using Myss.Api.Data;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Common.Auth;
    using Myss.Common.Content;
    using Myss.Data;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// Registration with who the account is for, over the real pipeline: the
    /// status codes and field errors the web client relies on, and a file
    /// uploaded through the attachments endpoint then linked to the account.
    /// </summary>
    public class AccountCreationEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string RegistrationSpec = """
            {
              "components": [
                { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
                { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
                { "type": "datetime", "key": "dateOfBirth", "input": true, "validate": { "required": true } },
                { "type": "email", "key": "email", "input": true, "validate": { "required": true } },
                { "type": "textfield", "key": "sin", "input": true, "properties": { "myssValidator": "sin" }, "validate": { "required": true } }
              ]
            }
            """;

        private static readonly object Answers = new
        {
            firstName = "Sam",
            lastName = "Rivera",
            dateOfBirth = "1980-07-15",
            email = "sam.rivera@example.com",
            sin = "046454286",
        };

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();
        private readonly FakeVirusScanProvider _scanner = new();
        private readonly InMemoryFileStorageProvider _storage = new();
        private readonly string _formsDbName = Guid.NewGuid().ToString();

        /// <summary>Initializes a new instance of the <see cref="AccountCreationEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public AccountCreationEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.VersionResult = FakeFormSpecProvider.Spec("registration", 5, RegistrationSpec);
        }

        [Fact]
        public async Task ALegalRepresentativeSignedInWithBcServicesCard_IsRefused()
        {
            using HttpClient client = CreateClient();

            using HttpResponseMessage response = await Register(client, "bcsc", new
            {
                context = "legalRepresentative",
                representative = new { firstName = "Pat", lastName = "Lee", phone = "(250) 555-0123" },
            });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            JsonElement error = await ErrorOn(response, "accountContext");
            Assert.Equal(ValidationKeywords.RegistrationContextSignInNotAllowed, error.GetProperty("keyword").GetString());
        }

        [Fact]
        public async Task AParentForMinor_WithBasicBceid_Registers()
        {
            using HttpClient client = CreateClient();

            using HttpResponseMessage response = await Register(client, "bceid", new
            {
                context = "parentForMinor",
                representative = new { firstName = "Pat", lastName = "Lee", phone = "(250) 555-0123" },
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AnAuthorizedHelper_WithoutAConsentForm_IsRefused()
        {
            using HttpClient client = CreateClient();

            using HttpResponseMessage response = await Register(client, "bcsc", new
            {
                context = "withHelper",
                helperAuthorized = true,
            });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            JsonElement error = await ErrorOn(response, "consentDocuments");
            Assert.Equal(ValidationKeywords.RegistrationDocumentConsentRequired, error.GetProperty("keyword").GetString());
        }

        [Fact]
        public async Task ARepresentativesInvalidPhone_IsWordedAsTheBrowserWordsIt()
        {
            using HttpClient client = CreateClient();

            using HttpResponseMessage response = await Register(client, "bceid", new
            {
                context = "legalRepresentative",
                representative = new { firstName = "Pat", lastName = "Lee", phone = "12345" },
            });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            JsonElement error = await ErrorOn(response, "representativePhone");
            Assert.Equal(ValidationKeywords.PhoneInvalidFormat, error.GetProperty("keyword").GetString());
            Assert.Equal("Phone number is invalid", error.GetProperty("message").GetString());
        }

        [Fact]
        public async Task AnUploadedDocument_IsLinkedToTheNewAccount()
        {
            using HttpClient client = CreateClient();
            using HttpResponseMessage upload = await client.SendAsync(Post("/v1/attachments", "bceid", PdfForm()));
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            using JsonDocument uploaded = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            Guid documentId = uploaded.RootElement.GetProperty("payload").GetProperty("id").GetGuid();

            using HttpResponseMessage response = await Register(client, "bceid", new
            {
                context = "legalRepresentative",
                representative = new { firstName = "Pat", lastName = "Lee", phone = "(250) 555-0123", agency = "Northern Trustee Services" },
                powerOfAttorneyDocumentIds = new[] { documentId },
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using FormsDbContext db = NewFormsDb();
            AccountDocument document = Assert.Single(await db.AccountDocuments.ToListAsync());
            Assert.Equal(documentId, document.AttachmentId);
            Assert.Equal(AccountDocumentPurpose.PowerOfAttorney, document.Purpose);
            Assert.Equal("Northern Trustee Services", Assert.Single(await db.AccountRepresentatives.ToListAsync()).Agency);
        }

        private static Task<HttpResponseMessage> Register(HttpClient client, string persona, object accountCreation)
        {
            bool basicBceid = persona == "bceid";
            HttpRequestMessage request = Post(
                "/v1/forms/registration/submissions",
                persona,
                JsonContent.Create(new
                {
                    formSpecVersion = 5,
                    answers = Answers,
                    pin = basicBceid ? "1357" : null,
                    pinConfirmation = basicBceid ? "1357" : null,
                    accountCreation,
                }));
            return client.SendAsync(request);
        }

        private static HttpRequestMessage Post(string path, string persona, HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
            request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            return request;
        }

        private static MultipartFormDataContent PdfForm()
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent("%PDF-1.7 test"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", "power-of-attorney.pdf");
            return form;
        }

        private static async Task<JsonElement> ErrorOn(HttpResponseMessage response, string field)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (JsonElement error in body.RootElement.GetProperty("payload").EnumerateArray())
            {
                if (error.GetProperty("field").GetString() == field)
                {
                    return error.Clone();
                }
            }

            throw new Xunit.Sdk.XunitException($"No error on {field}.");
        }

        private InMemoryFormsDbContext NewFormsDb() => new(FormsOptions());

        private DbContextOptions<FormsDbContext> FormsOptions() =>
            new DbContextOptionsBuilder<FormsDbContext>()
                .UseInMemoryDatabase(_formsDbName)
                .UseInternalServiceProvider(EfProvider.Value)
                .Options;

        private HttpClient CreateClient()
        {
            string attachmentsDbName = Guid.NewGuid().ToString();
            return _factory
                .WithWebHostBuilder(builder =>
                {
                    builder.UseMockAuthSettings(allowMockAuth: "true", environmentName: "test", mockAuth: "true");

                    // The suite switches every flag on; with EmailVerification on,
                    // registration first needs an address proven by an emailed link.
                    builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                        new Dictionary<string, string?> { [$"{FeatureFlags.SectionName}:{FeatureFlags.EmailVerification}"] = "false" }));

                    builder.ConfigureServices(services =>
                    {
                        DbContextOptions<FormsDbContext> formsOptions = FormsOptions();
                        services.RemoveAll<DbContextOptions<FormsDbContext>>();
                        services.RemoveAll<FormsDbContext>();
                        services.AddScoped<FormsDbContext>(_ => new InMemoryFormsDbContext(formsOptions));

                        ServiceProvider attachmentsEfProvider = new ServiceCollection()
                            .AddEntityFrameworkInMemoryDatabase()
                            .BuildServiceProvider();
                        services.RemoveAll<DbContextOptions<AttachmentsDbContext>>();
                        services.AddDbContext<AttachmentsDbContext>(options => options
                            .UseInMemoryDatabase(attachmentsDbName)
                            .UseInternalServiceProvider(attachmentsEfProvider));
                        services.RemoveAll<IVirusScanProvider>();
                        services.AddSingleton<IVirusScanProvider>(_scanner);
                        services.RemoveAll<IFileStorageProvider>();
                        services.AddSingleton<IFileStorageProvider>(_storage);

                        services.RemoveAll<IFormSpecProvider>();
                        services.AddSingleton<IFormSpecProvider>(_provider);
                        services.RemoveAll<IErrorMessageProvider>();
                        services.AddSingleton<IErrorMessageProvider>(new FakeErrorMessageProvider());
                    });
                })
                .CreateClient();
        }

        // The InMemory provider needs its own internal EF service provider: the
        // host's contexts still register Npgsql, and EF allows one database
        // provider per internal service provider. The forms one is shared, so
        // a test can read what the host saved.
        private static class EfProvider
        {
            public static readonly ServiceProvider Value = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();
        }
    }
}
