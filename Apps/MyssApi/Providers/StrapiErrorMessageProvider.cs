namespace Myss.Api.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Data;

    /// <summary>
    /// Reads the error message catalogue from the Strapi content engine over
    /// its REST API and caches it. The published rows are overlaid on the
    /// compiled defaults (<see cref="ErrorMessageDefaults"/>), so a keyword
    /// without a row keeps its default wording; when Strapi cannot be read the
    /// defaults alone are served and a refused submission is still worded.
    /// </summary>
    public class StrapiErrorMessageProvider : IErrorMessageProvider
    {
        private const string CacheKey = "error-message-catalogue";

        // One page is Strapi's rest.maxLimit (MyssContent/config/api.ts); a
        // larger request is silently capped to it, so paging is the only way to
        // be sure of reading the whole catalogue.
        private const int PageSize = 100;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        // The fallback is cached under a short TTL so a Strapi outage does not
        // make every refused submission retry Strapi (and wait out the
        // HttpClient timeout). Kept much shorter than the success TTL so a
        // recovered Strapi, or a freshly published rewording, is picked up
        // promptly.
        private static readonly TimeSpan FallbackCacheDuration = TimeSpan.FromSeconds(30);

        private readonly ILogger<StrapiErrorMessageProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiErrorMessageProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="cache">Injected in-memory cache.</param>
        /// <param name="configuration">Injected configuration provider.</param>
        public StrapiErrorMessageProvider(
            ILogger<StrapiErrorMessageProvider> logger,
            HttpClient httpClient,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _cache = cache;
            _httpClient.BaseAddress = new Uri(
                configuration.GetValue<string>("Strapi:BaseUrl") ?? "http://localhost:1337");

            // Same scoped read-only token as the form-spec and rate readers: the
            // Public role does not grant error-message find (see the MyssContent
            // bootstrap). An unset token means anonymous reads, which fall
            // through to the compiled defaults rather than serving nothing.
            string? apiToken = configuration.GetValue<string>("Strapi:ApiToken");
            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiToken.Trim());
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyDictionary<string, string>> GetMessagesAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
            {
                return cached;
            }

            try
            {
                IReadOnlyDictionary<string, string>? catalogue = await this.FetchAsync(cancellationToken);
                if (catalogue is not null)
                {
                    _cache.Set(CacheKey, catalogue, CacheDuration);
                    return catalogue;
                }
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Could not read the error message catalogue from the content engine; using the compiled defaults.");
            }

            IReadOnlyDictionary<string, string> fallback = ErrorMessageDefaults.Messages;
            _cache.Set(CacheKey, fallback, FallbackCacheDuration);
            return fallback;
        }

        /// <summary>
        /// Reads every published row, page by page, onto a copy of the defaults.
        /// Returns null when Strapi answers with an error status, so the caller
        /// falls back; a row that is not a keyword and a message is skipped and
        /// counted rather than allowed to blank a message.
        /// </summary>
        private async Task<IReadOnlyDictionary<string, string>?> FetchAsync(CancellationToken cancellationToken)
        {
            var catalogue = new Dictionary<string, string>(ErrorMessageDefaults.Messages, StringComparer.Ordinal);
            int skipped = 0;
            int page = 1;
            int pageCount;

            do
            {
                var query = new Uri(
                    "/api/error-messages?fields[0]=keyword&fields[1]=message&sort=keyword:asc"
                    + $"&pagination[page]={page.ToString(CultureInfo.InvariantCulture)}"
                    + $"&pagination[pageSize]={PageSize.ToString(CultureInfo.InvariantCulture)}",
                    UriKind.Relative);

                using HttpResponseMessage response = await _httpClient.GetAsync(query, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Content engine returned {StatusCode} for error-message page {Page}; using the compiled defaults",
                        (int)response.StatusCode,
                        page);
                    return null;
                }

                using JsonDocument document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken));

                if (!document.RootElement.TryGetProperty("data", out JsonElement data)
                    || data.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Content engine answered the error-message query without a data array; using the compiled defaults");
                    return null;
                }

                foreach (JsonElement entry in data.EnumerateArray())
                {
                    if (TryReadRow(entry, out string keyword, out string message))
                    {
                        catalogue[keyword] = message;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                pageCount = ReadPageCount(document.RootElement);
                page++;
            }
            while (page <= pageCount);

            if (skipped > 0)
            {
                _logger.LogWarning(
                    "Skipped {Skipped} error-message row(s) without a keyword and a message; their keywords keep the compiled wording",
                    skipped);
            }

            return catalogue;
        }

        private static bool TryReadRow(JsonElement entry, out string keyword, out string message)
        {
            keyword = string.Empty;
            message = string.Empty;

            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("keyword", out JsonElement keywordElement)
                || keywordElement.ValueKind != JsonValueKind.String
                || !entry.TryGetProperty("message", out JsonElement messageElement)
                || messageElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string? keywordValue = keywordElement.GetString();
            string? messageValue = messageElement.GetString();
            if (string.IsNullOrWhiteSpace(keywordValue) || string.IsNullOrWhiteSpace(messageValue))
            {
                return false;
            }

            keyword = keywordValue.Trim();
            message = messageValue.Trim();
            return true;
        }

        /// <summary>
        /// The number of pages Strapi reports; one when the body carries no
        /// pagination block, so a single-page answer is read exactly once.
        /// </summary>
        private static int ReadPageCount(JsonElement root)
        {
            if (root.TryGetProperty("meta", out JsonElement meta)
                && meta.ValueKind == JsonValueKind.Object
                && meta.TryGetProperty("pagination", out JsonElement pagination)
                && pagination.ValueKind == JsonValueKind.Object
                && pagination.TryGetProperty("pageCount", out JsonElement pageCount)
                && pageCount.ValueKind == JsonValueKind.Number
                && pageCount.TryGetInt32(out int count))
            {
                return Math.Max(count, 1);
            }

            return 1;
        }
    }
}
