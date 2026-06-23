namespace Myss.Api.Configuration.Addons.Observability
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Reflection;
    using Myss.Api.Configuration.Models;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using Serilog;
    using Serilog.Enrichers.Span;
    using Serilog.Events;
    using Serilog.Exceptions;
    using Serilog.Exceptions.Core;

    /// <summary>
    /// Methods to configure observability dependencies and settings.
    /// </summary>
    public static class ObservabilityServiceExtensions
    {
        /// <summary>
        /// Log output format template.
        /// </summary>
        public const string LogOutputTemplate =
            "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Configures logging with default settings.
        /// </summary>
        /// <param name="builder">A host builder.</param>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The host builder.</returns>
        public static IHostBuilder UseDefaultLogging(
            this IHostBuilder builder,
            string? serviceName = null
        )
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                serviceName = Assembly.GetEntryAssembly()!.GetName().Name!;
            }

            builder.UseSerilog(
                (ctx, config) => config.ConfigureDefaultLogging(ctx.Configuration, serviceName)
            );

            return builder;
        }

        /// <summary>
        /// Configures http request logging.
        /// </summary>
        /// <param name="app">An app builder.</param>
        /// <param name="excludePaths">Path to exclude - can use wildcards * for prefix or postfix.</param>
        /// <returns>The app builder.</returns>
        public static IApplicationBuilder UseDefaultHttpRequestLogging(
            this IApplicationBuilder app,
            string[]? excludePaths = null
        )
        {
            app.UseSerilogRequestLogging(opts =>
            {
                opts.IncludeQueryInRequestPath = true;

                // ReSharper disable once RedundantDelegateCreation
                opts.GetLevel = new Func<HttpContext, double, Exception?, LogEventLevel>(
                    (httpCtx, _, exception) => ExcludePaths(httpCtx, exception, excludePaths ?? [])
                );
                opts.EnrichDiagnosticContext = (diagCtx, httpCtx) =>
                {
                    diagCtx.Set("Host", httpCtx.Request.Host.Value);
                    diagCtx.Set(
                        "ContentLength",
                        httpCtx.Response.ContentLength?.ToString(CultureInfo.InvariantCulture)
                            ?? string.Empty
                    );
                    diagCtx.Set("Protocol", httpCtx.Request.Protocol);
                    diagCtx.Set("Scheme", httpCtx.Request.Scheme);
                };
            });

            return app;
        }

        /// <summary>
        /// Adds OpenTelemetry components to DI.
        /// </summary>
        /// <param name="services">A DI container.</param>
        /// <param name="otlpConfig">OpenTelemetry configuration values.</param>
        /// <returns>The DI container.</returns>
#pragma warning disable CA1506 //Avoid excessive class coupling
        public static IServiceCollection AddOpenTelemetryDefaults(
            this IServiceCollection services,
            OpenTelemetryConfig otlpConfig
        ) // NOSONAR
        {
            if (string.IsNullOrEmpty(otlpConfig.ServiceName))
            {
                otlpConfig.ServiceName = Assembly.GetEntryAssembly()!.GetName().Name;
            }

            if (string.IsNullOrEmpty(otlpConfig.ServiceVersion))
            {
                otlpConfig.ServiceVersion = Environment.GetEnvironmentVariable("VERSION");
            }

            services
                .AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder
                        .SetSampler(new AlwaysOnSampler())
                        .ConfigureResource(resourceBuilder =>
                            resourceBuilder.AddService(
                                otlpConfig.ServiceName
                                    ?? throw new InvalidOperationException(
                                        "OpenTelemetry:ServiceName must be configured."),
                                serviceVersion: otlpConfig.ServiceVersion
                            )
                        )
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            // ReSharper disable once RedundantLambdaParameterType
                            options.Filter = (HttpContext httpContext) =>
                                !Array.Exists(
                                    otlpConfig.IgnorePathPrefixes,
                                    s =>
                                        httpContext
                                            .Request.Path.ToString()
                                            .StartsWith(s, StringComparison.OrdinalIgnoreCase)
                                );
                        });

                    foreach (string source in otlpConfig.Sources)
                    {
                        builder.AddSource(source);
                    }

                    if (otlpConfig.TraceConsoleExporterEnabled)
                    {
                        builder.AddConsoleExporter();
                    }

                    if (otlpConfig.Endpoint != null)
                    {
                        builder.AddOtlpExporter(config =>
                        {
                            config.Protocol = otlpConfig.ExportProtocol;
                            config.Endpoint = otlpConfig.Endpoint;
                        });
                    }
                })
                .WithMetrics(builder =>
                {
                    builder
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddRuntimeInstrumentation();

                    if (otlpConfig.MetricsConsoleExporterEnabled)
                    {
                        builder.AddConsoleExporter();
                    }

                    if (otlpConfig.Endpoint != null)
                    {
                        builder.AddOtlpExporter(config =>
                        {
                            config.Protocol = otlpConfig.ExportProtocol;
                            config.Endpoint = otlpConfig.Endpoint;
                        });
                    }
                });

            services.AddSingleton(TracerProvider.Default.GetTracer(otlpConfig.ServiceName));
            return services;
        }
#pragma warning restore CA1506

        private static void ConfigureDefaultLogging(
            this LoggerConfiguration loggerConfiguration,
            IConfiguration configuration,
            string serviceName
        )
        {
            loggerConfiguration
                .Enrich.WithMachineName()
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder().WithDefaultDestructurers())
                .Enrich.WithProperty("Application", serviceName)
                .Enrich.WithEnvironmentName()
                .Enrich.WithCorrelationId()
                .Enrich.WithCorrelationIdHeader()
                .Enrich.WithRequestHeader("User-Agent")
                .Enrich.WithClientIp()
                .Enrich.WithSpan(
                    new SpanOptions
                    {
                        IncludeBaggage = true,
                        IncludeTags = true,
                        IncludeOperationName = true,
                        IncludeTraceFlags = true,
                    }
                )
                .ReadFrom.Configuration(configuration);
        }

        private static LogEventLevel ExcludePaths(
            HttpContext ctx,
            Exception? ex,
            string[] excludedPaths
        )
        {
            if (ex != null || ctx.Response.StatusCode >= (int)HttpStatusCode.InternalServerError)
            {
                return LogEventLevel.Error;
            }

            bool isWildcardMatch = Array.Exists(
                excludedPaths,
                path => IsWildcardMatch(ctx.Request.Path, path)
            );
            return isWildcardMatch ? LogEventLevel.Verbose : LogEventLevel.Information;
        }

        private static bool IsWildcardMatch(PathString requestPath, string path)
        {
            if (!requestPath.HasValue)
            {
                return false;
            }

            string requestPathValue = requestPath.Value!;

            return path switch
            {
                _ when path.EndsWith('*') => requestPathValue.StartsWith(
                    path.TrimEnd('*'),
                    StringComparison.InvariantCultureIgnoreCase
                ),
                _ when path.StartsWith('*') => requestPathValue.EndsWith(
                    path.TrimStart('*'),
                    StringComparison.InvariantCultureIgnoreCase
                ),
                _ => requestPath.Equals(path, StringComparison.InvariantCultureIgnoreCase),
            };
        }
    }
}
