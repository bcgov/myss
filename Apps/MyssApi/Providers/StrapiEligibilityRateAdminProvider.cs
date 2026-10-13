namespace Myss.Api.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;

    /// <summary>
    /// Writes eligibility rate tables to Strapi over its stock Content REST API,
    /// authenticated with the write-scoped <c>Strapi:AdminApiToken</c>:
    /// <list type="bullet">
    ///   <item><description>Same-day lookup: <c>GET /api/eligibility-rates?filters[effectiveDate][$eq]={date}&amp;status=draft</c></description></item>
    ///   <item><description>Create and publish: <c>POST /api/eligibility-rates?status=published</c></description></item>
    ///   <item><description>Update and publish: <c>PUT /api/eligibility-rates/{documentId}?status=published</c></description></item>
    /// </list>
    /// The rate rules are enforced by Strapi's <c>eligibility-rate</c> lifecycle, which
    /// runs on this path; the provider carries the request and relays any refusal.
    /// </summary>
    public class StrapiEligibilityRateAdminProvider : IEligibilityRateAdminProvider
    {
        private const string RatesPath = "/api/eligibility-rates";

        // The lifecycle's keyword for a second table with the same effective date.
        private const string DuplicateDateKeyword = "RATES.EFFECTIVE_DATE.DUPLICATE";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly ILogger<StrapiEligibilityRateAdminProvider> _logger;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiEligibilityRateAdminProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="configuration">Injected configuration provider.</param>
        public StrapiEligibilityRateAdminProvider(
            ILogger<StrapiEligibilityRateAdminProvider> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            string? baseUrl = configuration.GetValue<string>("Strapi:BaseUrl");
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException(
                    "Strapi:BaseUrl is not configured. Set it per environment (e.g., in appsettings.Development.json or as an environment variable).");
            }

            _httpClient.BaseAddress = new Uri(baseUrl);

            string? adminToken = configuration.GetValue<string>("Strapi:AdminApiToken");
            if (string.IsNullOrWhiteSpace(adminToken))
            {
                _logger.LogWarning(
                    "Strapi:AdminApiToken is not configured. Eligibility rate saves will be unauthenticated and will fail with 401/403.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", adminToken.Trim());
            }
        }

        /// <inheritdoc/>
        public async Task<SavedEligibilityRates> SaveTableAsync(
            string effectiveDate,
            IReadOnlyList<EligibilityRateRowModel> incomeRows,
            EligibilityAssetLimitsModel assetLimits,
            CancellationToken cancellationToken)
        {
            string? documentId = await FindDocumentIdAsync(effectiveDate, cancellationToken);
            if (documentId is not null)
            {
                return new SavedEligibilityRates(
                    await UpdateAsync(documentId, incomeRows, assetLimits, cancellationToken), Created: false);
            }

            try
            {
                return new SavedEligibilityRates(
                    await CreateAsync(effectiveDate, incomeRows, assetLimits, cancellationToken), Created: true);
            }
            catch (StrapiWriteException ex) when (ex.StatusCode == HttpStatusCode.BadRequest
                && ex.Body?.Contains(DuplicateDateKeyword, StringComparison.Ordinal) == true)
            {
                // Another save created today's table after the lookup; update it, so the later save wins.
                documentId = await FindDocumentIdAsync(effectiveDate, cancellationToken);
                if (documentId is null)
                {
                    throw;
                }

                return new SavedEligibilityRates(
                    await UpdateAsync(documentId, incomeRows, assetLimits, cancellationToken), Created: false);
            }
        }

        private async Task<EligibilityRatesModel> CreateAsync(
            string effectiveDate,
            IReadOnlyList<EligibilityRateRowModel> incomeRows,
            EligibilityAssetLimitsModel assetLimits,
            CancellationToken cancellationToken)
        {
            string body = JsonSerializer.Serialize(
                new { data = new { effectiveDate, incomeRows, assetLimits } }, Json);
            return ReadSavedTable(
                await SendAsync(HttpMethod.Post, $"{RatesPath}?status=published", body, cancellationToken));
        }

        // The date does not change, so an update sends only the amounts.
        private async Task<EligibilityRatesModel> UpdateAsync(
            string documentId,
            IReadOnlyList<EligibilityRateRowModel> incomeRows,
            EligibilityAssetLimitsModel assetLimits,
            CancellationToken cancellationToken)
        {
            string body = JsonSerializer.Serialize(new { data = new { incomeRows, assetLimits } }, Json);
            return ReadSavedTable(await SendAsync(
                HttpMethod.Put,
                $"{RatesPath}/{Uri.EscapeDataString(documentId)}?status=published",
                body,
                cancellationToken));
        }

        private static EligibilityRatesModel ReadSavedTable(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Object)
            {
                throw new ContentEngineUnavailableException("The content engine returned a malformed write response.");
            }

            try
            {
                return data.Deserialize<EligibilityRatesModel>(Json)
                    ?? throw new ContentEngineUnavailableException("The content engine returned an empty rate table.");
            }
            catch (JsonException ex)
            {
                throw new ContentEngineUnavailableException(
                    "The content engine returned a rate table in an unexpected shape.", ex);
            }
        }

        /// <summary>
        /// Finds the document holding the table dated <paramref name="effectiveDate"/>, or
        /// null when there is none. Strapi's lifecycle allows one table per date, so two
        /// documents mean the data is inconsistent, and nothing is written.
        /// </summary>
        private async Task<string?> FindDocumentIdAsync(string effectiveDate, CancellationToken cancellationToken)
        {
            string query = $"{RatesPath}?filters[effectiveDate][$eq]={Uri.EscapeDataString(effectiveDate)}"
                + "&status=draft&fields[0]=effectiveDate&pagination[limit]=2";
            JsonElement root = await SendAsync(HttpMethod.Get, query, body: null, cancellationToken);
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array)
            {
                throw new ContentEngineUnavailableException("The content engine returned a malformed list response.");
            }

            return data.GetArrayLength() switch
            {
                0 => null,
                1 when data[0].ValueKind == JsonValueKind.Object
                    && data[0].TryGetProperty("documentId", out JsonElement id)
                    && id.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(id.GetString()) => id.GetString(),
                1 => throw new ContentEngineUnavailableException(
                    "The content engine returned a rate table without a documentId."),
                _ => throw new ContentEngineUnavailableException(
                    $"The content engine holds more than one rate table dated {effectiveDate}."),
            };
        }

        /// <summary>
        /// Sends one request and returns the parsed response. A refused write throws
        /// <see cref="StrapiWriteException"/> with Strapi's body; a refused read, a
        /// transport failure, a timeout or a body that is not JSON throws
        /// <see cref="ContentEngineUnavailableException"/>. The caller's own
        /// cancellation propagates as a cancellation.
        /// </summary>
        private async Task<JsonElement> SendAsync(
            HttpMethod method, string url, string? body, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(method, url);
            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Could not reach the content engine to save eligibility rates.");
                throw new ContentEngineUnavailableException("The content engine could not be reached.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "The content engine did not respond in time to an eligibility rate save.");
                throw new ContentEngineUnavailableException("The content engine did not respond in time.", ex);
            }

            using (response)
            {
                string text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Content engine returned {StatusCode} for {Method} {Url}: {Body}",
                        (int)response.StatusCode,
                        method.Method,
                        url,
                        text);
                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning(
                            "Check that Strapi:AdminApiToken is set and grants find, findOne, create and update on eligibility-rate.");
                    }

                    if (method == HttpMethod.Get)
                    {
                        throw new ContentEngineUnavailableException(
                            $"The content engine returned {(int)response.StatusCode} for a read.");
                    }

                    throw new StrapiWriteException(response.StatusCode, text);
                }

                try
                {
                    using JsonDocument document = JsonDocument.Parse(text);
                    return document.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    throw new ContentEngineUnavailableException("The content engine returned a malformed response.", ex);
                }
            }
        }
    }
}
