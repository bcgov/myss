namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.Api.Configuration;
    using Myss.Api.Data;
    using Myss.Api.Domain;
    using Myss.Api.Intake;
    using Myss.Api.Platform;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// The applicant slice of Application Intake over real HTTP: ownership,
    /// the draft/submit rules, the row version, and the profile gate. These
    /// are the design's day-one invariants as executable assertions.
    /// </summary>
    public class IntakeEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Route = "/v1/intake/applications";

        private const string Spec = """
        {
          "display": "form",
          "components": [
            { "type": "textfield", "key": "firstName", "input": true, "validate": { "required": true } },
            { "type": "textfield", "key": "middleName", "input": true },
            { "type": "textfield", "key": "lastName", "input": true, "validate": { "required": true } },
            { "type": "button", "key": "submit", "action": "submit", "input": true }
          ]
        }
        """;

        private readonly WebApplicationFactory<Startup> _factory;
        private readonly FakeFormSpecProvider _provider = new();

        /// <summary>Initializes a new instance of the <see cref="IntakeEndpointTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public IntakeEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _provider.LatestResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 2, Spec);
            _provider.VersionResult = FakeFormSpecProvider.Spec(IntakeService.FormSpecId, 2, Spec);
        }

        [Fact]
        public async Task ARegisteredCitizen_CanStartADraft()
        {
            using Host host = NewHost(registered: ["alice"]);

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            JsonElement app = await Payload(response);
            Assert.Equal(ApplicationStatusCodes.Draft, app.GetProperty("status").GetString());
            Assert.Equal(1, app.GetProperty("version").GetInt32());
            Assert.Equal(2, app.GetProperty("formSpecVersion").GetInt32());
            Assert.Equal(JsonValueKind.Null, app.GetProperty("submittedAt").ValueKind);
        }

        [Fact]
        public async Task ACitizenWithoutAProfile_CannotStartAnApplication()
        {
            using Host host = NewHost(registered: []);

            using HttpResponseMessage response = await host.Send("bob", HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(IntakeKeywords.ProfileRequired, await Keyword(response));
            Assert.Equal(0, await host.CountApplications());
        }

        [Fact]
        public async Task AnotherCitizen_GetsNotFoundOnEveryRoute()
        {
            // The legacy attachment bug, encoded for applications: the owner is
            // part of the lookup, so someone else's id does not exist.
            using Host host = NewHost(registered: ["alice", "bob"]);
            Guid id = await host.Create("alice");

            using HttpResponseMessage get = await host.Send("bob", HttpMethod.Get, $"{Route}/{id}");
            using HttpResponseMessage save = await host.Send("bob", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Eve" }));
            using HttpResponseMessage submit = await host.Send("bob", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, new { firstName = "Eve", lastName = "X" }));

            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, submit.StatusCode);
        }

        [Fact]
        public async Task ADraftSaveMayBeIncomplete_ButASubmitMayNot()
        {
            using Host host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");

            using HttpResponseMessage save = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" }));
            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(2, new { firstName = "Ada" }));

            Assert.Equal(HttpStatusCode.OK, save.StatusCode);
            Assert.Equal(2, (await Payload(save)).GetProperty("version").GetInt32());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, submit.StatusCode);
            Assert.Equal(ValidationKeywords.FieldRequired, await FirstKeyword(submit, "lastName"));
            Assert.Equal(0, await host.CountEvents(id));
        }

        [Fact]
        public async Task ADraftSave_StillRefusesWhatTheFormCouldNotHaveProduced()
        {
            using Host host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");

            using HttpResponseMessage save = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada", isAdmin = true }));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, save.StatusCode);
            Assert.Equal(ValidationKeywords.FieldUnknown, await FirstKeyword(save, "isAdmin"));
        }

        [Fact]
        public async Task AStaleRowVersion_CannotSave()
        {
            using Host host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage first = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Ada" }));
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            using HttpResponseMessage stale = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Eve" }));

            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal(IntakeKeywords.Conflict, await Keyword(stale));
            Assert.Equal(2, (await Problem(stale)).GetProperty("currentVersion").GetInt32());
            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");
            Assert.Equal("Ada", (await Payload(get)).GetProperty("answers").GetProperty("firstName").GetString());
        }

        [Fact]
        public async Task Submit_AppendsOneEventAndMakesTheApplicationReadOnly()
        {
            using Host host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");

            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
            using HttpResponseMessage again = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
            using HttpResponseMessage save = await host.Send("alice", HttpMethod.Put, $"{Route}/{id}/answers", Answers(1, new { firstName = "Eve" }));

            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            JsonElement app = await Payload(submit);
            Assert.Equal(ApplicationStatusCodes.Submitted, app.GetProperty("status").GetString());
            Assert.NotEqual(JsonValueKind.Null, app.GetProperty("submittedAt").ValueKind);
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(again));
            Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
            Assert.Equal(IntakeKeywords.NotEditable, await Keyword(save));
            Assert.Equal(1, await host.CountEvents(id));
        }

        [Fact]
        public async Task AfterSubmit_ReadsServeWhatWasSubmittedEvenIfTheRowChanged()
        {
            using Host host = NewHost(registered: ["alice"]);
            Guid id = await host.Create("alice");
            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{id}/submit", Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

            // A stale tab, or anything else, scribbling on the working copy
            // after the fact must not change what the citizen submitted.
            await host.OverwriteRow(id, """{"firstName":"Eve","lastName":"Nobody"}""");

            using HttpResponseMessage get = await host.Send("alice", HttpMethod.Get, $"{Route}/{id}");
            JsonElement app = await Payload(get);
            Assert.Equal("Ada", app.GetProperty("answers").GetProperty("firstName").GetString());
            Assert.Equal(2, app.GetProperty("spec").GetProperty("version").GetInt32());
        }

        [Fact]
        public async Task TheList_ShowsOnlyTheCallersApplicationsWithTheirStatus()
        {
            using Host host = NewHost(registered: ["alice", "bob"]);
            Guid submitted = await host.Create("alice");
            Guid draft = await host.Create("alice");
            await host.Create("bob");
            using HttpResponseMessage submit = await host.Send("alice", HttpMethod.Post, $"{Route}/{submitted}/submit", Answers(1, new { firstName = "Ada", lastName = "Lovelace" }));
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

            using HttpResponseMessage list = await host.Send("alice", HttpMethod.Get, Route);

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var rows = (await Payload(list)).EnumerateArray().ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(draft, rows[0].GetProperty("id").GetGuid());
            Assert.Equal(ApplicationStatusCodes.Draft, rows[0].GetProperty("status").GetString());
            Assert.Equal(submitted, rows[1].GetProperty("id").GetGuid());
            Assert.Equal(ApplicationStatusCodes.Submitted, rows[1].GetProperty("status").GetString());
        }

        [Fact]
        public async Task AWorker_IsForbiddenOnTheApplicantRoutes()
        {
            using Host host = NewHost(registered: []);

            using HttpResponseMessage list = await host.Send("worker", HttpMethod.Get, Route);
            using HttpResponseMessage create = await host.Send("worker", HttpMethod.Post, Route);

            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        }

        [Fact]
        public async Task AnAnonymousCaller_IsUnauthorized()
        {
            using Host host = NewHost(registered: [], mockAuth: false);

            using HttpResponseMessage response = await host.Send(persona: null, HttpMethod.Get, Route);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private static JsonContent Answers(int version, object answers)
        {
            return JsonContent.Create(new { version, answers });
        }

        private static async Task<JsonElement> Payload(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").Clone();
        }

        private static async Task<JsonElement> Problem(HttpResponseMessage response)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.Clone();
        }

        private static async Task<string?> Keyword(HttpResponseMessage response)
        {
            return (await Problem(response)).GetProperty("keyword").GetString();
        }

        private static async Task<string?> FirstKeyword(HttpResponseMessage response, string field)
        {
            foreach (JsonElement error in (await Payload(response)).EnumerateArray())
            {
                if (error.GetProperty("field").GetString() == field)
                {
                    return error.GetProperty("keyword").GetString();
                }
            }

            return null;
        }

        private Host NewHost(IReadOnlyList<string> registered, bool mockAuth = true)
        {
            return new Host(_factory, _provider, registered, mockAuth);
        }

        /// <summary>
        /// One in-memory host per test: the three contexts the slice touches
        /// (forms for the profile, platform for the log, intake for the row)
        /// on the InMemory provider, the fake spec provider, and the mock
        /// personas. Every persona in <c>registered</c> gets a profile row.
        /// </summary>
        private sealed class Host : IDisposable
        {
            private readonly WebApplicationFactory<Startup> _factory;
            private readonly HttpClient _client;

            public Host(
                WebApplicationFactory<Startup> factory,
                FakeFormSpecProvider provider,
                IReadOnlyList<string> registered,
                bool mockAuth)
            {
                string dbName = Guid.NewGuid().ToString();
                ServiceProvider efProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();
                DbContextOptions<FormsDbContext> forms = new DbContextOptionsBuilder<FormsDbContext>()
                    .UseInMemoryDatabase(dbName + "-forms")
                    .UseInternalServiceProvider(efProvider)
                    .Options;
                DbContextOptions<PlatformDbContext> platform = new DbContextOptionsBuilder<PlatformDbContext>()
                    .UseInMemoryDatabase(dbName + "-platform")
                    .UseInternalServiceProvider(efProvider)
                    .Options;
                DbContextOptions<IntakeDbContext> intake = new DbContextOptionsBuilder<IntakeDbContext>()
                    .UseInMemoryDatabase(dbName + "-intake")
                    .UseInternalServiceProvider(efProvider)
                    .Options;

                using (var db = new InMemoryFormsDbContext(forms))
                {
                    foreach (string persona in registered)
                    {
                        db.MyssUserProfiles.Add(new MyssUserProfile
                        {
                            Id = Guid.NewGuid(),
                            Subject = MockAuthenticationHandler.Personas[persona].Subject,
                            FirstName = persona,
                            LastName = "Tester",
                            DateOfBirth = new DateOnly(1990, 1, 1),
                            Email = $"{persona}@example.com",
                            Sin = "050082833",
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow,
                        });
                    }

                    db.SaveChanges();
                }

                _factory = factory.WithWebHostBuilder(builder =>
                {
                    string enabled = mockAuth ? "true" : "false";
                    builder.UseMockAuthSettings(allowMockAuth: enabled, environmentName: "test", mockAuth: enabled);
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<DbContextOptions<FormsDbContext>>();
                        services.RemoveAll<FormsDbContext>();
                        services.AddScoped<FormsDbContext>(_ => new InMemoryFormsDbContext(forms));

                        services.RemoveAll<DbContextOptions<PlatformDbContext>>();
                        services.RemoveAll<PlatformDbContext>();
                        services.AddScoped<PlatformDbContext>(_ => new InMemoryPlatformDbContext(platform));

                        services.RemoveAll<DbContextOptions<IntakeDbContext>>();
                        services.RemoveAll<IntakeDbContext>();
                        services.AddScoped<IntakeDbContext>(_ => new InMemoryIntakeDbContext(intake));

                        services.RemoveAll<IFormSpecProvider>();
                        services.AddSingleton<IFormSpecProvider>(provider);
                    });
                });
                _client = _factory.CreateClient();
            }

            public Task<HttpResponseMessage> Send(string? persona, HttpMethod method, string url, HttpContent? content = null)
            {
                var request = new HttpRequestMessage(method, url) { Content = content };
                if (persona is not null)
                {
                    request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
                }

                return _client.SendAsync(request);
            }

            public async Task<Guid> Create(string persona)
            {
                using HttpResponseMessage response = await Send(persona, HttpMethod.Post, Route);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                return (await Payload(response)).GetProperty("id").GetGuid();
            }

            public async Task<int> CountEvents(Guid streamId)
            {
                using IServiceScope scope = _factory.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
                    .Events.CountAsync(e => e.StreamId == streamId);
            }

            public async Task<int> CountApplications()
            {
                using IServiceScope scope = _factory.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IntakeDbContext>().Applications.CountAsync();
            }

            public async Task OverwriteRow(Guid id, string answersJson)
            {
                using IServiceScope scope = _factory.Services.CreateScope();
                IntakeDbContext db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
                ApplicationAnswers row = await db.Applications.SingleAsync(a => a.Id == id);
                row.Answers = JsonDocument.Parse(answersJson);
                row.Version++;
                await db.SaveChangesAsync();
            }

            public void Dispose()
            {
                _client.Dispose();
                _factory.Dispose();
            }
        }
    }
}
