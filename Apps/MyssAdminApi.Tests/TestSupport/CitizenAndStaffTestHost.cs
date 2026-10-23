extern alias citizen;

namespace Myss.AdminApi.Tests.TestSupport
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Tests.TestDoubles;
    using Myss.Data;
    using Myss.Data.Intake;
    using Myss.Data.Platform;
    using Myss.Common.Auth;
    using Myss.Common.Content;
    using CitizenErrorMessageProvider = citizen::Myss.Api.Providers.IErrorMessageProvider;
    using CitizenStartup = citizen::Myss.Api.Startup;

    /// <summary>
    /// The two services a file passes through, over one in-memory copy of the
    /// shared schemas: MyssApi, where a citizen creates and submits an
    /// application, and this API, where a worker reviews it. The three contexts
    /// the slice touches (forms for the profile, platform for the log, intake for
    /// the row) are the same stores in both hosts, as the two share one database
    /// when deployed. Requests are routed by path: the worker routes to this API,
    /// everything else to MyssApi. Every persona in <c>registered</c> gets a
    /// citizen profile row.
    /// </summary>
    public sealed class CitizenAndStaffTestHost : IDisposable
    {
        private readonly WebApplicationFactory<CitizenStartup> _citizenFactory;
        private readonly WebApplicationFactory<Startup> _staffFactory;
        private readonly HttpClient _citizen;
        private readonly HttpClient _staff;

        /// <summary>Initializes a new instance of the <see cref="CitizenAndStaffTestHost"/> class.</summary>
        /// <param name="staffFactory">The shared host factory for this API.</param>
        /// <param name="provider">The fake spec provider both services read specs through.</param>
        /// <param name="registered">Personas that get a profile row.</param>
        /// <param name="mockAuth">Whether mock auth is on; off makes every call anonymous.</param>
        /// <param name="promotion">The promotion step, when a test needs other than the placeholder's success.</param>
        public CitizenAndStaffTestHost(
            WebApplicationFactory<Startup> staffFactory,
            FakeFormSpecProvider provider,
            IReadOnlyList<string> registered,
            bool mockAuth,
            IApplicationPromotionProvider? promotion = null)
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

            string enabled = mockAuth ? "true" : "false";

            void SharedStores(IServiceCollection services)
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
            }

            _citizenFactory = new WebApplicationFactory<CitizenStartup>().WithWebHostBuilder(builder =>
            {
                builder.UseMockAuthSettings(allowMockAuth: enabled, environmentName: "test", mockAuth: enabled);
                builder.ConfigureServices(services =>
                {
                    SharedStores(services);

                    // Both APIs read specs through the shared IFormSpecProvider
                    // (MyssCommon): one fake, so both see one content engine.
                    services.RemoveAll<IFormSpecProvider>();
                    services.AddSingleton<IFormSpecProvider>(provider);

                    // The compiled defaults, so a refused save or submit never
                    // reaches for a content engine that is not running here.
                    services.RemoveAll<CitizenErrorMessageProvider>();
                    services.AddSingleton<CitizenErrorMessageProvider>(new CompiledErrorMessages());
                });
            });

            _staffFactory = staffFactory.WithWebHostBuilder(builder =>
            {
                builder.UseMockAuthSettings(allowMockAuth: enabled, environmentName: "test", mockAuth: enabled);
                builder.ConfigureServices(services =>
                {
                    SharedStores(services);

                    services.RemoveAll<IFormSpecProvider>();
                    services.AddSingleton<IFormSpecProvider>(provider);

                    if (promotion is not null)
                    {
                        services.RemoveAll<IApplicationPromotionProvider>();
                        services.AddSingleton(promotion);
                    }
                });
            });

            _citizen = _citizenFactory.CreateClient();
            _staff = _staffFactory.CreateClient();
        }

        /// <summary>
        /// Sends a request as a persona (null for anonymous) to a relative API path:
        /// the worker routes to this API, every other route to MyssApi.
        /// </summary>
        public Task<HttpResponseMessage> Send(string? persona, HttpMethod method, string relativePath, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, relativePath) { Content = content };
            if (persona is not null)
            {
                request.Headers.Add(MockAuthenticationHandler.PersonaHeader, persona);
            }

            bool staffRoute = relativePath.StartsWith("/v1/intake/review/", StringComparison.Ordinal)
                || relativePath == "/v1/intake/review/applications";
            return (staffRoute ? _staff : _citizen).SendAsync(request);
        }

        /// <summary>Creates an application as a citizen persona; asserts 201 and returns its id.</summary>
        public async Task<Guid> Create(string persona)
        {
            using HttpResponseMessage response = await Send(persona, HttpMethod.Post, "/v1/intake/applications");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("payload").GetProperty("id").GetGuid();
        }

        /// <summary>Submits an application as a citizen persona; asserts 200.</summary>
        public async Task Submit(string persona, Guid id, int version, object answers)
        {
            using HttpResponseMessage response = await Send(
                persona,
                HttpMethod.Post,
                $"/v1/intake/applications/{id}/submit",
                JsonContent.Create(new { version, answers }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>The event types on a stream, oldest first.</summary>
        public async Task<IReadOnlyList<string>> EventTypes(Guid streamId)
        {
            using IServiceScope scope = _staffFactory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
                .Events.Where(e => e.StreamId == streamId)
                .OrderBy(e => e.Version)
                .Select(e => e.Type)
                .ToListAsync();
        }

        /// <summary>The number of events on a stream.</summary>
        public async Task<int> CountEvents(Guid streamId)
        {
            using IServiceScope scope = _staffFactory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
                .Events.CountAsync(e => e.StreamId == streamId);
        }

        /// <summary>The actor of the newest event on a stream.</summary>
        public async Task<string?> LastActor(Guid streamId)
        {
            using IServiceScope scope = _staffFactory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
                .Events.Where(e => e.StreamId == streamId)
                .OrderByDescending(e => e.Version)
                .Select(e => e.Actor)
                .FirstOrDefaultAsync();
        }

        /// <summary>Disposes both hosts.</summary>
        public void Dispose()
        {
            _citizen.Dispose();
            _staff.Dispose();
            _citizenFactory.Dispose();
            _staffFactory.Dispose();
        }

        private sealed class CompiledErrorMessages : CitizenErrorMessageProvider
        {
            public Task<IReadOnlyDictionary<string, string>> GetMessagesAsync(CancellationToken cancellationToken) =>
                Task.FromResult(citizen::Myss.Api.Data.ErrorMessageDefaults.Messages);
        }
    }
}
