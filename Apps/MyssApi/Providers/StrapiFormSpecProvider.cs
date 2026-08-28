namespace Myss.Api.Providers
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
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

            // Strapi's Public role no longer carries form-spec.find/findOne, so
            // reads are authenticated with a scoped read-only API token. The
            // token is deliberately NOT defaulted: an unset value must fail
            // loudly as a 403 from Strapi rather than silently fall back to
            // anonymous access that only works while someone forgot to revoke
            // the public grant.
            string? apiToken = configuration.GetValue<string>("Strapi:ApiToken");
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                _logger.LogWarning(
                    "Strapi:ApiToken is not configured. Form-spec reads will be anonymous and will fail "
                    + "unless the Public role still grants form-spec find/findOne.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiToken.Trim());
            }
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

                if (response.StatusCode == HttpStatusCode.Unauthorized
                    || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    // The single most likely cause, and otherwise a slow diagnosis:
                    // the token is missing, mistyped, revoked, or lacks find/findOne
                    // on form-spec.
                    _logger.LogWarning(
                        "The content engine rejected the request as unauthorized. Check that Strapi:ApiToken "
                        + "is set and that the token grants find and findOne on form-spec.");
                }
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
