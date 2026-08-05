namespace Myss.Api.Providers
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;

    /// <summary>
    /// Reads form specs from the Strapi content engine over its REST API.
    /// Each published entry holds one version of a spec.
    /// </summary>
    public class StrapiFormSpecProvider : IFormSpecProvider
    {
        private readonly ILogger<StrapiFormSpecProvider> _logger;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiFormSpecProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="configuration">Injected configuration provider.</param>
        public StrapiFormSpecProvider(
            ILogger<StrapiFormSpecProvider> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(configuration.GetValue<string>("Strapi:BaseUrl") ?? "http://localhost:1337");
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetLatestAsync(string formSpecId, CancellationToken cancellationToken)
        {
            string query = $"/api/form-specs?filters[formSpecId][$eq]={Uri.EscapeDataString(formSpecId)}"
                + "&sort=version:desc&pagination[limit]=1";
            return FetchFirstAsync(query, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<FormSpecModel?> GetVersionAsync(string formSpecId, int version, CancellationToken cancellationToken)
        {
            string query = $"/api/form-specs?filters[formSpecId][$eq]={Uri.EscapeDataString(formSpecId)}"
                + $"&filters[version][$eq]={version}&pagination[limit]=1";
            return FetchFirstAsync(query, cancellationToken);
        }

        private async Task<FormSpecModel?> FetchFirstAsync(string query, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(query, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Content engine returned {StatusCode} for form-spec query {Query}",
                    (int)response.StatusCode,
                    query);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            JsonElement data = document.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement entry = data[0];
            return new FormSpecModel
            {
                FormSpecId = entry.GetProperty("formSpecId").GetString()!,
                Version = entry.GetProperty("version").GetInt32(),
                Title = entry.TryGetProperty("title", out JsonElement title) ? title.GetString() : null,
                Spec = entry.GetProperty("spec").Clone(),
            };
        }
    }
}
