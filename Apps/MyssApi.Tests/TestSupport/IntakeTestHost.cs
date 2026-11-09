namespace Myss.Api.Tests.TestSupport
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.Api.Configuration;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Data;
    using Myss.Api.Intake;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Data;
    using Myss.Data.Intake;
    using Myss.Data.Platform;
    using Myss.FormSpec.Domain;
    using Myss.Common.Auth;
    using Myss.Common.Content;

    /// <summary>
    /// One in-memory host per test: the three contexts the slice touches
    /// (forms for the profile, platform for the log, intake for the row)
    /// on the InMemory provider, the fake spec provider, and the mock
    /// personas. Every persona in <c>registered</c> gets a profile row.
    /// </summary>
    public sealed class IntakeTestHost : IDisposable
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly HttpClient _client;
        private readonly OneShotPause _beforeAppend = new();
        private readonly OneShotPause _afterAppend = new();

        /// <summary>Initializes a new instance of the <see cref="IntakeTestHost"/> class.</summary>
        /// <param name="factory">The shared host factory.</param>
        /// <param name="provider">The fake spec provider every context shares.</param>
        /// <param name="registered">Personas that get a profile row.</param>
        /// <param name="mockAuth">Whether mock auth is on; off makes every call anonymous.</param>
        /// <param name="email">The email provider, when a test reads what was sent; otherwise a fresh fake.</param>
        public IntakeTestHost(
            WebApplicationFactory<Startup> factory,
            FakeFormSpecProvider provider,
            IReadOnlyList<string> registered,
            bool mockAuth,
            FakeEmailProvider? email = null)
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

                        // As the migration left every profile from before verification (MYSS-305).
                        EmailVerifiedAt = DateTimeOffset.UtcNow,
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

                    // The real store, with pauses a race test can arm.
                    services.RemoveAll<IEventStore>();
                    services.AddScoped<IEventStore>(sp => new PausableEventStore(
                        ActivatorUtilities.CreateInstance<EventStore>(sp),
                        _beforeAppend,
                        _afterAppend));

                    services.RemoveAll<DbContextOptions<IntakeDbContext>>();
                    services.RemoveAll<IntakeDbContext>();
                    services.AddScoped<IntakeDbContext>(_ => new InMemoryIntakeDbContext(intake));

                    services.RemoveAll<IFormSpecProvider>();
                    services.AddSingleton<IFormSpecProvider>(provider);

                    // The compiled defaults, so a refused save or submit never
                    // reaches for a content engine that is not running here.
                    services.RemoveAll<IErrorMessageProvider>();
                    services.AddSingleton<IErrorMessageProvider>(new FakeErrorMessageProvider());

                    // Nothing here sends real email, and the reset link needs a base URL.
                    services.RemoveAll<IEmailProvider>();
                    services.AddSingleton<IEmailProvider>(email ?? new FakeEmailProvider());
                    services.RemoveAll<PinResetConfig>();
                    services.AddSingleton(new PinResetConfig { LinkBaseUrl = new Uri("https://myss.test") });
                });
            });
            _client = _factory.CreateClient();
        }

        /// <summary>Sends a request as a persona (null for anonymous) to a relative API path.</summary>
        public Task<HttpResponseMessage> Send(string? persona, HttpMethod method, string relativePath, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, relativePath) { Content = content };
            if (persona is not null)
            {
                request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            }

            return _client.SendAsync(request);
        }

        /// <summary>
        /// Holds the next event append, before it reaches the store, until
        /// released. Submit and withdrawal both claim the answers row before
        /// they append, so this parks either one between those two writes.
        /// </summary>
        /// <returns>A task that completes once the append is held, and the source that releases it.</returns>
        public (Task Reached, TaskCompletionSource Release) PauseNextAppend() => _beforeAppend.Arm();

        /// <summary>
        /// Holds the next event append, once it has been written, until
        /// released: a submit parked here has its event in the log but has
        /// not yet copied its answers into the row.
        /// </summary>
        /// <returns>A task that completes once the append is held, and the source that releases it.</returns>
        public (Task Reached, TaskCompletionSource Release) PauseAfterNextAppend() => _afterAppend.Arm();

        public async Task<Guid> Create(string persona)
        {
            using HttpResponseMessage response = await Send(persona, HttpMethod.Post, "/v1/intake/applications");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").GetProperty("id").GetGuid();
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

        /// <summary>Reads the row's working copy straight from the database, past the projection's choice of answers.</summary>
        public async Task<JsonElement> RowAnswers(Guid id)
        {
            using IServiceScope scope = _factory.Services.CreateScope();
            ApplicationAnswers row = await scope.ServiceProvider.GetRequiredService<IntakeDbContext>()
                .Applications.AsNoTracking().SingleAsync(a => a.Id == id);
            return row.Answers.RootElement.Clone();
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
