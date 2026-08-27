namespace Myss.Api.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Data;
    using Myss.Api.Models;

    /// <summary>
    /// Reads the eligibility rate table from the Strapi content engine over its
    /// REST API and caches it. When Strapi cannot be read the compiled MYSS-25
    /// values (<see cref="FddRateData.August2023"/>) are returned so the public
    /// estimator keeps working; the estimate is identical either way.
    /// </summary>
    public class StrapiEligibilityRateProvider : IEligibilityRateProvider
    {
        private const string CacheKey = "eligibility-rate-table";

        // The latest published entry: newest effective date first, one row.
        private const string RatesQuery =
            "/api/eligibility-rates?sort=effectiveDate:desc&pagination[limit]=1";

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private readonly ILogger<StrapiEligibilityRateProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiEligibilityRateProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="cache">Injected in-memory cache.</param>
        /// <param name="configuration">Injected configuration provider.</param>
        public StrapiEligibilityRateProvider(
            ILogger<StrapiEligibilityRateProvider> logger,
            HttpClient httpClient,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _cache = cache;
            _httpClient.BaseAddress = new Uri(
                configuration.GetValue<string>("Strapi:BaseUrl") ?? "http://localhost:1337");

            // Same scoped read-only token as the form-spec reader: the Public role
            // no longer grants eligibility-rate find/findOne (see the MyssContent
            // bootstrap). An unset token means anonymous reads, which fall through
            // to the compiled fallback rather than serving blank rates.
            string? apiToken = configuration.GetValue<string>("Strapi:ApiToken");
            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiToken.Trim());
            }
        }

        /// <inheritdoc/>
        public async Task<EligibilityRatesModel> GetRatesAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(CacheKey, out EligibilityRatesModel? cached) && cached is not null)
            {
                return cached;
            }

            try
            {
                EligibilityRatesModel? rates = await this.FetchAsync(cancellationToken);
                if (rates is not null)
                {
                    _cache.Set(CacheKey, rates, CacheDuration);
                    return rates;
                }

                _logger.LogWarning(
                    "The content engine returned no published eligibility-rate entry; using the compiled fallback.");
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Could not read the eligibility rate table from the content engine; using the compiled fallback.");
            }

            return Fallback();
        }

        private static EligibilityRatesModel Map(JsonElement entry)
        {
            var rows = new List<EligibilityRateRowModel>();
            foreach (JsonElement row in entry.GetProperty("incomeRows").EnumerateArray())
            {
                rows.Add(new EligibilityRateRowModel
                {
                    FamilySize = row.GetProperty("familySize").GetInt32(),
                    A = row.GetProperty("a").GetDecimal(),
                    B = row.GetProperty("b").GetDecimal(),
                    C = row.GetProperty("c").GetDecimal(),
                    D = row.GetProperty("d").GetDecimal(),
                    E = row.GetProperty("e").GetDecimal(),
                });
            }

            JsonElement limits = entry.GetProperty("assetLimits");
            var assetLimits = new EligibilityAssetLimitsModel
            {
                A = limits.GetProperty("a").GetDecimal(),
                B = limits.GetProperty("b").GetDecimal(),
                C = limits.GetProperty("c").GetDecimal(),
                D = limits.GetProperty("d").GetDecimal(),
            };

            string effectiveDate = entry.TryGetProperty("effectiveDate", out JsonElement date)
                ? date.GetString() ?? string.Empty
                : string.Empty;

            return new EligibilityRatesModel
            {
                EffectiveDate = effectiveDate,
                IncomeRows = rows,
                AssetLimits = assetLimits,
            };
        }

        private static EligibilityRatesModel Fallback()
        {
            List<EligibilityRateRowModel> rows = FddRateData.RateRows
                .Select(row => new EligibilityRateRowModel
                {
                    FamilySize = row.FamilySize,
                    A = row.TypeA,
                    B = row.TypeB,
                    C = row.TypeC,
                    D = row.TypeD,
                    E = row.TypeE,
                })
                .ToList();

            Dictionary<string, decimal> byCategory =
                FddRateData.AssetLimits.ToDictionary(limit => limit.LimitType, limit => limit.Limit);

            return new EligibilityRatesModel
            {
                EffectiveDate = FddRateData.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IncomeRows = rows,
                AssetLimits = new EligibilityAssetLimitsModel
                {
                    A = byCategory["A"],
                    B = byCategory["B"],
                    C = byCategory["C"],
                    D = byCategory["D"],
                },
            };
        }

        private async Task<EligibilityRatesModel?> FetchAsync(CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(RatesQuery, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "The content engine returned {StatusCode} for the eligibility rate table.",
                    (int)response.StatusCode);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document =
                await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            JsonElement data = document.RootElement.GetProperty("data");
            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            {
                return null;
            }

            return Map(data[0]);
        }
    }
}
