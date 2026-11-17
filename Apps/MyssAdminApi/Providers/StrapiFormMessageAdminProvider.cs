namespace Myss.AdminApi.Providers
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
    using Myss.Common.Content;

    /// <summary>
    /// Reads form message entries through <see cref="FormMessageReader"/>
    /// and creates them over Strapi's stock Content REST API
    /// (<c>POST /api/form-messages?status=published</c>), authenticated with
    /// the write-scoped <c>Strapi:AdminApiToken</c>.
    /// </summary>
    public class StrapiFormMessageAdminProvider : IFormMessageAdminProvider
    {
        private const string CreatePath = "/api/form-messages?status=published";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly ILogger<StrapiFormMessageAdminProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly FormMessageReader _reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiFormMessageAdminProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="httpClient">Injected HTTP client for writes.</param>
        /// <param name="configuration">Injected configuration provider.</param>
        /// <param name="reader">Injected content engine reader.</param>
        public StrapiFormMessageAdminProvider(
            ILogger<StrapiFormMessageAdminProvider> logger,
            HttpClient httpClient,
            IConfiguration configuration,
            FormMessageReader reader)
        {
            _logger = logger;
            _httpClient = httpClient;
            _reader = reader;
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
                    "Strapi:AdminApiToken is not configured. Form message saves will be unauthenticated and will fail with 401/403.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", adminToken.Trim());
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<FormMessageEntry>> ReadNewestAsync(
            string formSpecId,
            string messageKey,
            int count,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _reader.ReadNewestAsync(formSpecId, messageKey, count, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException
                || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                throw new ContentEngineUnavailableException(
                    "Could not read the form messages from the content engine.", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<FormMessageEntry> CreateAsync(
            string formSpecId,
            string messageKey,
            FormMessageTexts texts,
            string changedBy,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(texts);

            string body = JsonSerializer.Serialize(
                new { data = new { formSpecId, messageKey, texts.Heading, texts.Title, texts.Body, changedBy } }, Json);
            using HttpRequestMessage request = new(HttpMethod.Post, CreatePath)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Could not reach the content engine to save a form message.");
                throw new ContentEngineUnavailableException("The content engine could not be reached.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "The content engine did not respond in time to a form message save.");
                throw new ContentEngineUnavailableException("The content engine did not respond in time.", ex);
            }

            using (response)
            {
                string text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Content engine returned {StatusCode} for a form message save: {Body}",
                        (int)response.StatusCode,
                        text);
                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning(
                            "Check that Strapi:AdminApiToken is set and grants find, findOne and create on form-message.");
                    }

                    throw new StrapiWriteException(response.StatusCode, text);
                }

                return ReadCreatedEntry(text);
            }
        }

        private static FormMessageEntry ReadCreatedEntry(string text)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("data", out JsonElement data)
                    && FormMessageReader.ReadEntry(data) is FormMessageEntry entry)
                {
                    return entry;
                }
            }
            catch (JsonException ex)
            {
                throw new ContentEngineUnavailableException("The content engine returned a malformed response.", ex);
            }

            throw new ContentEngineUnavailableException("The content engine returned a form message in an unexpected shape.");
        }
    }
}
