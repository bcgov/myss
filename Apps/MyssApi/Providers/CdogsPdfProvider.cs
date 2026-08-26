namespace Myss.Api.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// CDOGS provider for rendering ODT templates into PDF.
    /// </summary>
    public class CdogsPdfProvider : IPdfProvider
    {
        private readonly ILogger<CdogsPdfProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _tokenEndpoint;
        private readonly string _clientId;
        private readonly string _clientSecret;

        /// <summary>
        /// Initializes a new instance of the <see cref="CdogsPdfProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected logger provider.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="configuration">Injected configuration provider.</param>
        public CdogsPdfProvider(
            ILogger<CdogsPdfProvider> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _baseUrl = configuration["Cdogs:BaseUrl"] ?? string.Empty;
            _tokenEndpoint = configuration["Cdogs:TokenEndpoint"] ?? string.Empty;
            _clientId = configuration["Cdogs:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Cdogs:ClientSecret"] ?? string.Empty;
        }

        public async Task<byte[]> GenerateFromOdtAsync(
            byte[] odtTemplate,
            object data,
            CancellationToken cancellationToken)
        {
            ValidateConfiguration();

            if (odtTemplate.Length == 0)
            {
                throw new ArgumentException("The ODT template cannot be empty.", nameof(odtTemplate));
            }

            string accessToken = await GetAccessTokenAsync(cancellationToken);
            string renderUrl = $"{_baseUrl.TrimEnd('/')}/template/render";

            var payload = new
            {
                data,
                template = new
                {
                    content = Convert.ToBase64String(odtTemplate),
                    fileType = "odt",
                    encodingType = "base64",
                },
                options = new
                {
                    convertTo = "pdf",
                    overwrite = true,
                    reportName = "myss-report",
                },
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, renderUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Rendered PDF via CDOGS endpoint {RenderUrl}", renderUrl);
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var tokenPayload = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(tokenPayload),
            };
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            JsonDocument body = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!body.RootElement.TryGetProperty("access_token", out JsonElement tokenElement))
            {
                throw new InvalidOperationException("CDOGS token response did not include access_token.");
            }

            string? accessToken = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("CDOGS access_token was empty.");
            }

            return accessToken;
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)
                || string.IsNullOrWhiteSpace(_tokenEndpoint)
                || string.IsNullOrWhiteSpace(_clientId)
                || string.IsNullOrWhiteSpace(_clientSecret))
            {
                throw new InvalidOperationException(
                    "Cdogs settings are not configured (BaseUrl, TokenEndpoint, ClientId, ClientSecret).");
            }
        }
    }
}