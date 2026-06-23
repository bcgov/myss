namespace Myss.Api.Configuration
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using Myss.Api.Configuration.Addons.Observability;
    using Myss.Api.Configuration.Addons.Swagger;
    using Myss.Api.Configuration.Models;
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
