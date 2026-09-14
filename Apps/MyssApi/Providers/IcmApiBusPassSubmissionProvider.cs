namespace Myss.Api.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Models;
    using Polly.CircuitBreaker;
    using Polly.Timeout;

    /// <summary>
    /// <see cref="IBusPassSubmissionProvider"/> over the ICM middleware's REST
    /// contract. The middleware holds the ICM credentials and does the Siebel
    /// translation; this class carries a business-language request across HTTP
    /// and brings the outcome back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registered as a singleton over <see cref="IHttpClientFactory"/> rather
    /// than as a typed client: a typed client is transient, and the service
    /// token cached below would otherwise be fetched once per request.
    /// </para>
    /// <para>
    /// Authentication is a client-credentials token for the middleware, fetched
    /// on first use and cached until shortly before it expires, the arrangement
    /// <see cref="CdogsPdfProvider"/> uses for CDOGS. Retry, circuit breaking and
    /// timeouts live on the named HttpClient (see
    /// <c>Configuration/IcmApiResilience</c>), not in here.
    /// </para>
    /// </remarks>
    public sealed class IcmApiBusPassSubmissionProvider : IBusPassSubmissionProvider, IDisposable
    {
        /// <summary>
        /// The named HttpClient this provider asks the factory for.
        /// </summary>
        public const string HttpClientName = "IcmApi";

        /// <summary>
        /// The middleware route this provider posts to, relative to <c>IcmApi:BaseUrl</c>.
        /// </summary>
        public const string ApplicationsPath = "v1/bus-pass/applications";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        // Refresh a little before the issuer's expiry so a token never dies mid-call.
        private static readonly TimeSpan TokenExpirySafetyMargin = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromMinutes(5);

        private readonly ILogger<IcmApiBusPassSubmissionProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IcmApiAuthConfig _auth;
        private readonly TimeProvider _timeProvider;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);
        private CachedToken? _token;

        /// <summary>
        /// Initializes a new instance of the <see cref="IcmApiBusPassSubmissionProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="httpClientFactory">Injected client factory; the <see cref="HttpClientName"/> client carries the base address and resilience.</param>
        /// <param name="config">Injected middleware settings.</param>
        /// <param name="timeProvider">Injected clock, for token expiry.</param>
        public IcmApiBusPassSubmissionProvider(
            ILogger<IcmApiBusPassSubmissionProvider> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<IcmApiConfig> config,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _auth = config.Value.Auth;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public async Task<BusPassSubmissionOutcomeModel> SubmitAsync(
            BusPassApplicationModel application,
            CancellationToken cancellationToken)
        {
            using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
            string accessToken = await GetAccessTokenAsync(client, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Post, ApplicationsPath)
            {
                Content = JsonContent.Create(application, options: JsonOptions),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using HttpResponseMessage response = await SendAsync(client, request, cancellationToken);
            int status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                // A 4xx here means the two sides disagree about the contract,
                // which is a defect to fix rather than a condition to retry; for
                // the citizen the request is undeliverable either way.
                _logger.LogError("ICM middleware answered {StatusCode} to a bus pass submission", status);
                throw new IcmApiUnavailableException($"The ICM middleware answered {status}.")
                {
                    StatusCode = status,
                };
            }

            BusPassSubmissionOutcomeModel? outcome;
            try
            {
                outcome = await response.Content.ReadFromJsonAsync<BusPassSubmissionOutcomeModel>(
                    JsonOptions,
                    cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new IcmApiUnavailableException(
                    "The ICM middleware returned a body this API could not read.",
                    ex)
                {
                    StatusCode = status,
                };
            }

            return outcome
                ?? throw new IcmApiUnavailableException(
                    "The ICM middleware reported success but returned no outcome.")
                {
                    StatusCode = status,
                };
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _tokenLock.Dispose();
        }

        private static async Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await client.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new IcmApiUnavailableException("The ICM middleware could not be reached.", ex);
            }
            catch (TimeoutRejectedException ex)
            {
                throw new IcmApiUnavailableException("The ICM middleware did not answer in time.", ex);
            }
            catch (BrokenCircuitException ex)
            {
                throw new IcmApiUnavailableException(
                    "The ICM middleware has been failing and calls to it are paused.",
                    ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient.Timeout surfaces as a cancellation nobody asked for.
                throw new IcmApiUnavailableException("The ICM middleware did not answer in time.", ex);
            }
        }

        private async Task<string> GetAccessTokenAsync(HttpClient client, CancellationToken cancellationToken)
        {
            if (TryGetCachedToken(out string? cached))
            {
                return cached;
            }

            // Single-flight: a cold cache under load becomes one token request,
            // not one per caller.
            await _tokenLock.WaitAsync(cancellationToken);
            try
            {
                if (TryGetCachedToken(out cached))
                {
                    return cached;
                }

                if (!_auth.IsConfigured)
                {
                    throw new InvalidOperationException(
                        "IcmApi:Auth is not configured (TokenEndpoint, ClientId and ClientSecret are all required). "
                        + "Locally: set them in appsettings.local.json (see appsettings.local.sample.json). "
                        + "Deployed: set Myss_IcmApi__Auth__TokenEndpoint/ClientId/ClientSecret from the secret.");
                }

                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _auth.ClientId!,
                    ["client_secret"] = _auth.ClientSecret!,
                };
                if (!string.IsNullOrWhiteSpace(_auth.Scope))
                {
                    form["scope"] = _auth.Scope;
                }

                DateTimeOffset issuedAt = _timeProvider.GetUtcNow();
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_auth.TokenEndpoint!))
                {
                    Content = new FormUrlEncodedContent(form),
                };
                using HttpResponseMessage response = await SendAsync(client, request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // invalid_client lands here as a 401: the clearest signal a
                    // deployment has the wrong secret, so it is logged as an error.
                    int status = (int)response.StatusCode;
                    _logger.LogError("Token endpoint for the ICM middleware answered {StatusCode}", status);
                    throw new IcmApiUnavailableException(
                        $"The token endpoint for the ICM middleware answered {status}.")
                    {
                        StatusCode = status,
                    };
                }

                CachedToken token = await ReadTokenAsync(response, issuedAt, cancellationToken);
                _token = token;
                return token.Value;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private static async Task<CachedToken> ReadTokenAsync(
            HttpResponseMessage response,
            DateTimeOffset issuedAt,
            CancellationToken cancellationToken)
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument body = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!body.RootElement.TryGetProperty("access_token", out JsonElement tokenElement)
                || tokenElement.GetString() is not { Length: > 0 } token)
            {
                throw new IcmApiUnavailableException(
                    "The token endpoint for the ICM middleware did not return an access_token.");
            }

            TimeSpan lifetime =
                body.RootElement.TryGetProperty("expires_in", out JsonElement expiresIn)
                && expiresIn.TryGetInt32(out int seconds)
                    ? TimeSpan.FromSeconds(seconds)
                    : DefaultTokenLifetime;

            return new CachedToken(token, issuedAt + lifetime - TokenExpirySafetyMargin);
        }

        private bool TryGetCachedToken([NotNullWhen(true)] out string? token)
        {
            CachedToken? cached = _token;
            if (cached is not null && _timeProvider.GetUtcNow() < cached.ExpiresAt)
            {
                token = cached.Value;
                return true;
            }

            token = null;
            return false;
        }

        // One reference so the value and its expiry are always read together.
        private sealed record CachedToken(string Value, DateTimeOffset ExpiresAt);
    }
}
