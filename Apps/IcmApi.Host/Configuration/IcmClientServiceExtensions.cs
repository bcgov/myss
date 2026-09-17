namespace Icm.Api.Host.Configuration
{
    using System;
    using System.Net.Http;
    using Icm.Api.Host.Configuration.Models;
    using Icm.Api.Host.Services;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Registers the ICM client library the way its README describes, plus the
    /// submitter this host puts in front of it.
    /// </summary>
    public static class IcmClientServiceExtensions
    {
        /// <summary>The named HttpClient that carries calls to ICM.</summary>
        public const string IcmHttpClientName = "Icm";

        /// <summary>The named HttpClient that carries calls to ICM's token endpoint.</summary>
        public const string TokenHttpClientName = "IcmToken";

        /// <summary>
        /// Adds the ICM client and the bus pass submitter.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration to read the <c>Icm</c> section from.</param>
        /// <param name="logger">The startup logger.</param>
        /// <returns>The service collection.</returns>
        /// <exception cref="InvalidOperationException">The non-secret ICM settings are missing.</exception>
        public static IServiceCollection AddIcmClient(
            this IServiceCollection services,
            IConfiguration configuration,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);

            services.Configure<IcmConfig>(configuration.GetSection("Icm"));
            IcmConfig icm = new();
            configuration.GetSection("Icm").Bind(icm);

            if (!icm.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Icm is not configured (BaseUrl, and either Auth:TokenUrl or Auth:BaseUrl + Auth:Realm, are required). "
                    + "Locally: `dotnet user-secrets set \"Icm:BaseUrl\" …` from Apps/IcmApi.Host (the store is shared with IcmApi.Console), "
                    + "or appsettings.local.json. Deployed: set Icm_Icm__BaseUrl and Icm_Icm__Auth__Realm (or Icm_Icm__Auth__TokenUrl).");
            }

            if (!icm.Auth.HasCredentials)
            {
                // Booting without the secret is allowed so tests and a first local run
                // work; a submission then answers 503 with a keyword rather than 500.
                logger.LogWarning(
                    "Icm:Auth:ClientId/ClientSecret are not set. Submissions will be refused with {Keyword} until they are.",
                    Contracts.BusPassKeywords.NotConfigured);
            }

            logger.LogInformation(
                "ICM client configured. BaseUrl: {BaseUrl}, TokenUrl: {TokenUrl}, TrustedUserName set: {TrustedUserNameSet}, Timeout: {TimeoutSeconds}s, token timeout: {TokenTimeoutSeconds}s",
                icm.BaseUrl,
                icm.Auth.ResolveTokenUrl(),
                !string.IsNullOrWhiteSpace(icm.TrustedUserName),
                icm.TimeoutSeconds,
                icm.Auth.ResolveTokenUrl() is null ? 0 : icm.TokenTimeoutSeconds);

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
            services.AddTransient<CorrelationIdForwardingHandler>();

            services
                .AddHttpClient(IcmHttpClientName, client =>
                {
                    client.BaseAddress = icm.BaseUrl;
                    client.Timeout = TimeSpan.FromSeconds(icm.TimeoutSeconds);
                })
                .AddHttpMessageHandler<CorrelationIdForwardingHandler>();

            // Its own, shorter budget: on a cold cache the token call precedes the
            // ICM call, and the two together must finish inside MyssApi's attempt.
            services.AddHttpClient(TokenHttpClientName, client =>
                client.Timeout = TimeSpan.FromSeconds(icm.TokenTimeoutSeconds));

            // The token cache is a singleton on purpose (per-request would fetch a
            // token per call). The repository builds one client per token URL and
            // keeps it, so the factory is consulted once.
            services.AddSingleton<IOAuthTokenRepository>(provider =>
            {
                IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
                return new OAuthTokenRepository(tokenUrl =>
                {
                    HttpClient client = factory.CreateClient(TokenHttpClientName);
                    client.BaseAddress = tokenUrl;
                    return client;
                });
            });
            services.AddSingleton<IOAuthTokenService>(provider => new OAuthTokenService(
                provider.GetRequiredService<IOAuthTokenRepository>(),
                timeProvider: provider.GetRequiredService<TimeProvider>()));

            // Transient so each call gets a factory-managed HttpClient (handler
            // rotation, DNS changes) rather than one held for the process lifetime.
            services.AddTransient<IBusPassRepository>(provider => new BusPassRepository(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(IcmHttpClientName),
                icm.TrustedUserName,
                provider.GetRequiredService<TimeProvider>()));

            services.AddTransient<IBusPassSubmitter, IcmBusPassSubmitter>();

            return services;
        }
    }
}
