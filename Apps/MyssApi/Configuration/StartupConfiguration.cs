namespace Myss.Api.Configuration
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Myss.Api.Configuration.Addons.Observability;
    using Myss.Api.Configuration.Addons.Swagger;
    using Myss.Api.Configuration.Models;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    /// <summary>
    /// Class that provides startup configuration for webhosting.
    /// </summary>
    public class StartupConfiguration
    {
        private readonly IWebHostEnvironment environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartupConfiguration"/> class.
        /// </summary>
        /// <param name="config">The configuration provider.</param>
        /// <param name="env">The environment variables provider.</param>
        public StartupConfiguration(IConfiguration config, IWebHostEnvironment env)
        {
            this.environment = env;
            this.Configuration = config;
            this.Logger = ProgramConfiguration.GetInitialLogger(this.Configuration);
        }

        /// <summary>
        /// Gets the startup configuration.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the Startup Logger.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Configures the swagger services.
        /// </summary>
        /// <param name="services">The service collection provider.</param>
        public void ConfigureSwaggerServices(IServiceCollection services)
        {
            services.Configure<SwaggerConfig>(Configuration.GetSection("Swagger"));
            string xmlPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            Assembly callingAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
            Assembly executingAssembly = Assembly.GetExecutingAssembly();

            // Calling Assembly (Core App) + References + Executing Assembly (Common) References
            string[] xmlDocs = new[] { callingAssembly.GetName() }
                .Union(callingAssembly.GetReferencedAssemblies())
                .Union(executingAssembly.GetReferencedAssemblies())
                .Select(a => Path.Combine(xmlPath, $"{a.Name}.xml"))
                .Where(File.Exists)
                .ToArray();

            services
                .AddApiVersionWithExplorer()
                .AddSwaggerOptions()
                .AddSwaggerGen(options =>
                {
                    Array.ForEach(xmlDocs, d => options.IncludeXmlComments(d));
                    options.UseAllOfForInheritance();
                    options.UseOneOfForPolymorphism();
                    options.SchemaFilter<SwaggerExcludeModelFilter>();
                    options.SchemaFilter<SwaggerGenericFilter>();
                    options.DocumentFilter<SwaggerExcludeModelFilter>();
                    options.CustomSchemaIds(type =>
                        type.ToString()
                            .Replace("`1", "")
                            .Replace("IEnumerable", "List")
                            .Replace("[", "")
                            .Replace("]", "")
                    );
                });
        }

        /// <summary>
        /// Configures authentication. <b>This method is the Option 1 / Option 2 swap point.</b>
        /// <para>
        /// Option 1 (current): the API is a stateless resource server — every request carries a
        /// bearer token which is validated against Keycloak's JWKS. Option 2 (BFF) replaces the
        /// body of this method with <c>AddCookie</c> + <c>AddOpenIdConnect</c>; the lines marked
        /// SHARED below move across verbatim, and nothing outside this method changes.
        /// </para>
        /// <para>
        /// When the mock gate is open (local dev / tests) a fake scheme is registered instead,
        /// so the whole app is exercisable before IDIM confirms the real Keycloak details.
        /// </para>
        /// </summary>
        /// <param name="services">The service collection provider.</param>
        public void ConfigureAuthentication(IServiceCollection services)
        {
            // Throws if a production-named environment has the mock flags set.
            if (MockAuthGate.Evaluate(Configuration))
            {
                Logger.LogWarning(
                    "MOCK AUTHENTICATION ENABLED - all requests are signed in as a development persona. This must never happen outside local development.");

                services
                    .AddAuthentication(MockAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, MockAuthenticationHandler>(
                        MockAuthenticationHandler.SchemeName, _ => { });

                return;
            }

            string? authority = Configuration["Oidc:Authority"];
            string? clientId = Configuration["Oidc:ClientId"];
            KeycloakRoleSource roleSource =
                Enum.TryParse(Configuration["Oidc:RoleSource"], true, out KeycloakRoleSource parsed)
                    ? parsed
                    : KeycloakRoleSource.Both;

            // Defaults to the client id, since the SPA and the API share one client
            // under Option 1. Override Oidc:Audience if a dedicated API audience is
            // ever issued (e.g. when moving to Option 2/BFF). Setting Oidc:Audience
            // to "" is an explicit opt-out for a realm that really does mint
            // aud:"account", leaving the azp check as the only guard.
            string? audience = Configuration["Oidc:Audience"] ?? clientId;

            Logger.LogInformation(
                "Configuring JWT bearer authentication. Authority: {Authority}, ClientId: {ClientId}, Audience: {Audience}, RoleSource: {RoleSource}",
                authority,
                clientId,
                audience,
                roleSource);

            if (string.IsNullOrWhiteSpace(audience))
            {
                Logger.LogWarning(
                    "Oidc:Audience resolved empty - audience validation is DISABLED. The azp check is the only guard that the token was issued to this application.");
            }

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authority;          // JWKS discovered from here
                    options.RequireHttpsMetadata = true;
                    options.MapInboundClaims = false;       // SHARED: keep raw sub/roles/bceid claims
                    options.TokenValidationParameters.NameClaimType = "sub";                        // SHARED
                    options.TokenValidationParameters.RoleClaimType = KeycloakClaims.RolesClaimType; // SHARED

                    // Verified against a real IDIR access token from this client
                    // (2026-07-27): aud == azp == "sdpr-my-ss-6498". The earlier
                    // assumption that BC Gov standard-realm tokens carry aud:"account"
                    // does NOT hold here, so audience validation is enabled properly
                    // rather than disabled and leaning on azp alone. The azp check
                    // below is kept as defence in depth, not replaced.
                    if (string.IsNullOrWhiteSpace(audience))
                    {
                        options.TokenValidationParameters.ValidateAudience = false;
                    }
                    else
                    {
                        options.TokenValidationParameters.ValidateAudience = true;
                        options.TokenValidationParameters.ValidAudience = audience;
                    }

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            if (!string.IsNullOrWhiteSpace(clientId))
                            {
                                string? azp = context.Principal?.FindFirst("azp")?.Value;
                                if (!string.Equals(azp, clientId, StringComparison.Ordinal))
                                {
                                    context.Fail(
                                        "Token was not issued to this application (azp mismatch).");
                                    return Task.CompletedTask;
                                }
                            }

                            // SHARED: flatten Keycloak's nested roles so policies can see them.
                            if (context.Principal is not null)
                            {
                                KeycloakClaims.MapInto(context.Principal, clientId, roleSource);
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
        }

        ///
        /// <summary>
        /// Configures the http services.
        /// </summary>
        /// <param name="services">The service collection provider.</param>
        public void ConfigureHttpServices(IServiceCollection services)
        {
            Logger.LogDebug("Configure Http Services...");

            services.AddResponseCompression(options => options.EnableForHttps = true);

            services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();

            services.AddHealthChecks();

            services
                .AddRazorPages()
                .AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);
        }

        /// <summary>
        /// Configures Forward proxies.
        /// </summary>
        /// <param name="services">The service collection to add forward proxies into.</param>
        public void ConfigureForwardHeaders(IServiceCollection services)
        {
            IConfigurationSection section = Configuration.GetSection("ForwardProxies");
            bool enabled = section.GetValue<bool>("Enabled");
            Logger.LogInformation("Forward proxies enabled: {ProxiesEnabled}", enabled);
            if (enabled)
            {
                Logger.LogDebug("Configuring forwarded headers");
                IPAddress[] proxyIPs = section.GetSection("KnownProxies").Get<IPAddress[]>() ?? [];
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.All;
                    options.RequireHeaderSymmetry = false;
                    options.ForwardLimit = null;
                    options.KnownIPNetworks.Clear();
                    options.KnownProxies.Clear();
                    foreach (IPAddress ip in proxyIPs)
                    {
                        options.KnownProxies.Add(ip);
                    }
                });
            }
        }

        /// <summary>
        /// Configures the app to use x-forwarded-for headers to obtain the real client IP.
        /// </summary>
        /// <param name="app">The application builder provider.</param>
        public void UseForwardHeaders(IApplicationBuilder app)
        {
            IConfigurationSection section = Configuration.GetSection("ForwardProxies");
            bool enabled = section.GetValue<bool>("Enabled");
            Logger.LogInformation("Forward proxies enabled: {ProxiesEnabled}", enabled);
            if (enabled)
            {
                string basePath = section.GetValue<string>("BasePath") ?? string.Empty;
                if (!string.IsNullOrEmpty(basePath))
                {
                    Logger.LogInformation("Setting PathBase for app to {BasePath}", basePath);
                    app.UsePathBase(basePath);
                    app.Use(
                        async (context, next) =>
                        {
                            context.Request.PathBase = basePath;
                            await next.Invoke();
                        }
                    );
                    app.UsePathBase(basePath);
                }

                Logger.LogInformation("Enabling Use Forwarded Headers");
                app.UseForwardedHeaders();
            }
        }

        /// <summary>
        /// Configures OpenTelemetry tracing.
        /// </summary>
        /// <param name="services">The service collection to add forward proxies into.</param>
        public void ConfigureTracing(IServiceCollection services)
        {
            OpenTelemetryConfig otlpConfig = new();
            Configuration.GetSection("OpenTelemetry").Bind(otlpConfig);
            if (otlpConfig.Enabled)
            {
                Logger.LogInformation("Configuring OpenTelemetry");
                services.AddOpenTelemetryDefaults(otlpConfig);
            }
            else
            {
                Logger.LogWarning("OpenTelemetry is disabled");
            }
        }

        /// <summary>
        /// Configures the app to use http.
        /// </summary>
        /// <param name="app">The application builder provider.</param>
        /// <param name="useExceptionPage">
        /// If true, app will use development exception page. Should be false when using problem
        /// details middleware.
        /// </param>
        public void UseHttp(IApplicationBuilder app, bool useExceptionPage = true)
        {
            if (!environment.IsDevelopment())
            {
                app.UseResponseCompression();
            }

            if (useExceptionPage && environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            RequestLoggingConfig requestLoggingconfig = new();
            Configuration.GetSection("RequestLogging").Bind(requestLoggingconfig);
            if (requestLoggingconfig.Enabled)
            {
                app.UseDefaultHttpRequestLogging(requestLoggingconfig.ExcludedPaths?.ToArray());
            }

            app.UseRouting();

            // Enable health endpoint for readiness probe
            app.UseHealthChecks("/health");

            // CORS. AllowOrigins is "*" (any origin) or a comma-separated explicit list.
            string? enableCors = Configuration.GetValue<string>("AllowOrigins");
            if (!string.IsNullOrEmpty(enableCors))
            {
                app.UseCors(builder =>
                {
                    if (enableCors == "*")
                    {
                        builder.AllowAnyOrigin();
                    }
                    else
                    {
                        var origins = enableCors.Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                        );
                        builder.WithOrigins(origins);
                    }

                    builder.AllowAnyHeader().AllowAnyMethod();
                });
            }

            // Authentication/authorization sit after routing and CORS but before the
            // endpoints are executed (UseRest). Option 2 uses this same slot.
            app.UseAuthentication();
            app.UseAuthorization();

            // Setup response secure headers
            app.Use(
                async (context, next) =>
                {
                    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.Append("X-Xss-Protection", "1; mode=block");
                    await next();
                }
            );
        }

        ///
        /// <summary>
        /// Enables response caching and sets default no cache.
        /// </summary>
        /// <param name="app">The application build provider.</param>
        public void UseResponseCaching(IApplicationBuilder app)
        {
            Logger.LogDebug("Setting up Response Cache");
            app.UseResponseCaching();

            app.Use(
                async (context, next) =>
                {
                    context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                    {
                        NoCache = true,
                        NoStore = true,
                        MustRevalidate = true,
                    };
                    context.Response.Headers[HeaderNames.Pragma] = new StringValues("no-cache");
                    await next();
                }
            );
        }

        /// <summary>
        /// Configures the app to use swagger.
        /// </summary>
        /// <param name="app">The application builder provider.</param>
        public void UseSwagger(IApplicationBuilder app)
        {
            Logger.LogDebug("Use Swagger...");

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        /// <summary>
        /// Configures the app to use Rest services.
        /// </summary>
        /// <param name="app">The application builder provider.</param>
        public void UseRest(IApplicationBuilder app)
        {
            Logger.LogDebug("Use Rest...");
            app.UseEndpoints(routes => routes.MapControllers());
        }

        /// <summary>
        /// Configures the app to use middleware to enrich tracing telemetry with additional properties.
        /// </summary>
        /// <param name="app">The application builder provider.</param>
        public void UseEnrichTracing(IApplicationBuilder app)
        {
            OpenTelemetryConfig openTelemetryConfig = new();
            Configuration.GetSection("OpenTelemetry").Bind(openTelemetryConfig);

            if (openTelemetryConfig.Enabled)
            {
                app.Use(
                    async (context, next) =>
                    {
                        string user = context.User.Identity?.Name ?? string.Empty;
                        EnrichActivityWithBaggage("User", user, Activity.Current);

                        await next();
                    }
                );
            }
        }

        private static void EnrichActivityWithBaggage(string key, string value, Activity? activity)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            activity?.AddBaggage(key, value);
        }
    }
}
