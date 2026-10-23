namespace Myss.Common.Content
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Myss.FormSpec;

    /// <summary>
    /// Reads published form specs from the Strapi content engine over its REST API.
    /// Each published entry holds one version of a spec. MyssApi reads the spec a
    /// citizen fills in; MyssAdminApi reads the version an application or
    /// submission was made on, for the worker screens.
    /// </summary>
    public class StrapiFormSpecProvider : IFormSpecProvider
    {
        private readonly ILogger<StrapiFormSpecProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _tokenSettingName;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiFormSpecProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="options">The content engine address and this API's token.</param>
        public StrapiFormSpecProvider(
            ILogger<StrapiFormSpecProvider> logger,
            HttpClient httpClient,
            StrapiReadOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _logger = logger;
            _httpClient = httpClient;
            _tokenSettingName = options.TokenSettingName;
            options.ApplyTo(_httpClient);

            // Strapi's Public role no longer carries form-spec.find/findOne, so
            // reads must carry a token (see StrapiReadOptions).
            if (options.ApiToken is null)
            {
                _logger.LogWarning(
                    "{TokenSetting} is not configured. Form-spec reads will be anonymous and will fail "
                    + "unless the Public role still grants form-spec find/findOne.",
                    _tokenSettingName);
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
            using HttpResponseMessage response =
                await _httpClient.GetAsync(new Uri(query, UriKind.Relative), cancellationToken);
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
                        "The content engine rejected the request as unauthorized. Check that {TokenSetting} "
                        + "is set and that the token grants find and findOne on form-spec.",
                        _tokenSettingName);
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
