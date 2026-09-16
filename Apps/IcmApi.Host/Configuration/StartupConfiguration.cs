namespace Icm.Api.Host.Configuration
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Icm.Api.Host.Configuration.Models;
    using Icm.Api.Host.Services;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.OpenApi.Models;
    using Serilog;
    using Serilog.Events;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    /// <summary>
    /// The host's service and pipeline configuration, in the same shape as
    /// MyssApi's so the two read alike.
    /// </summary>
    public class StartupConfiguration
    {
        private readonly IWebHostEnvironment environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartupConfiguration"/> class.
        /// </summary>
        /// <param name="config">The configuration provider.</param>
        /// <param name="env">The environment provider.</param>
        public StartupConfiguration(IConfiguration config, IWebHostEnvironment env)
        {
            this.environment = env;
            this.Configuration = config;
            this.Logger = ProgramConfiguration.GetInitialLogger(config);
        }

        /// <summary>Gets the configuration.</summary>
        public IConfiguration Configuration { get; }

        /// <summary>Gets the startup logger.</summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Configures controllers, JSON, problem details and health checks.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureHttpServices(IServiceCollection services)
        {
            services.AddHealthChecks();
            services.AddHttpContextAccessor();
            services.AddProblemDetails();

            services
                .AddControllers()
                .AddJsonOptions(options =>
                {
                    // Enums travel as their names ("NewApplication"), which is what
                    // MyssApi writes; reading is case-insensitive either way.
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });
        }

        /// <summary>
        /// Configures the OpenAPI document. It is the middleware's published
        /// contract: the "Siebel REST middleware API reference" the handbook names.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureSwaggerServices(IServiceCollection services)
        {
            string xmlDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string[] xmlDocs = Directory
                .EnumerateFiles(xmlDirectory, "IcmApi*.xml")
                .ToArray();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MySS ICM middleware",
                    Version = "v1",
                    Description = "The business-language contract MySS uses for anything ICM-bound. "
                        + "Callers never see Siebel's shape; this service does the translation.",
                });
                Array.ForEach(xmlDocs, doc => options.IncludeXmlComments(doc));
            });
        }

        /// <summary>
        /// Configures authentication: a bearer token from the shared standard realm,
        /// issued to one of the allowed clients. When the mock gate is open a fake
        /// scheme is registered instead, so the host can be exercised locally
        /// without a service-account token.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureAuthentication(IServiceCollection services)
        {
            if (MockAuthGate.Evaluate(this.Configuration))
            {
                this.Logger.LogWarning(
                    "MOCK AUTHENTICATION ENABLED - every request is accepted as a development caller. This must never happen outside local development.");

                services
                    .AddAuthentication(MockAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, MockAuthenticationHandler>(
                        MockAuthenticationHandler.SchemeName, _ => { });

                return;
            }

            OidcConfig oidc = new();
            this.Configuration.GetSection("Oidc").Bind(oidc);
            if (!oidc.IsConfigured)
            {
                // Fail closed: with no allow-list, any client of the realm could
                // file bus pass requests through this host.
                throw new InvalidOperationException(
                    "Oidc is not configured (Authority and at least one AllowedClients entry are required). "
                    + "Deployed: set Icm_Oidc__Authority and Icm_Oidc__AllowedClients__0 to the calling application's client id. "
                    + "Locally: appsettings.json carries the dev realm defaults, or open the mock gate (see appsettings.local.sample.json).");
            }

            string[] allowedClients = [.. oidc.AllowedClients.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())];

            this.Logger.LogInformation(
                "Configuring JWT bearer authentication. Authority: {Authority}, Audience: {Audience}, AllowedClients: {AllowedClients}",
                oidc.Authority,
                oidc.Audience ?? "(not validated)",
                string.Join(", ", allowedClients));

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = oidc.Authority;
                    options.RequireHttpsMetadata = true;
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.NameClaimType = "sub";

                    // A client-credentials token from the standard realm carries the
                    // requesting client as its audience, so audience validation only
                    // helps once a dedicated audience mapper exists. Until then the
                    // azp allow-list below is the guard, as MyssApi's own comment on
                    // the realm's aud/azp behaviour anticipates.
                    if (string.IsNullOrWhiteSpace(oidc.Audience))
                    {
                        options.TokenValidationParameters.ValidateAudience = false;
                    }
                    else
                    {
                        options.TokenValidationParameters.ValidateAudience = true;
                        options.TokenValidationParameters.ValidAudience = oidc.Audience;
                    }

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            string? azp = context.Principal?.FindFirst("azp")?.Value;
                            if (azp is null || !allowedClients.Contains(azp, StringComparer.Ordinal))
                            {
                                context.Fail("Token was not issued to an application allowed to call this middleware (azp not allowed).");
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
        }

        /// <summary>
        /// Requires an authenticated caller on every endpoint that does not opt out.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureAuthorization(IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
        }

        /// <summary>
        /// Configures the request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        public void UseHttp(IApplicationBuilder app)
        {
            if (this.environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler();
            }

            // The correlation id goes on before request logging so every line of a
            // request, the summary included, carries it.
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseSerilogRequestLogging(options => options.GetLevel = RequestLogLevel);

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                await next();
            });

            this.UseSwagger(app);
            UseEndpoints(app);
        }

        private static LogEventLevel RequestLogLevel(HttpContext context, double elapsed, Exception? exception)
        {
            if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                return LogEventLevel.Error;
            }

            // The readiness probe would otherwise be most of the log.
            return context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        }

        private static void UseEndpoints(IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                // The readiness probe needs no token.
                endpoints.MapHealthChecks("/health").AllowAnonymous();
            });
        }

        private void UseSwagger(IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.RoutePrefix = this.Configuration["Swagger:RoutePrefix"] ?? "swagger";
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "MySS ICM middleware v1");
            });
        }
    }
}
