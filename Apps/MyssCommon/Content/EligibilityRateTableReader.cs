namespace Myss.Common.Content
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Reads the eligibility rate table from the Strapi content engine over its REST
    /// API and checks it is in the nine-column shape and complete. It neither caches
    /// nor falls back: MyssApi's citizen provider caches it and serves
    /// <see cref="CompiledTable"/> when Strapi cannot be read, while the admin editor
    /// reports Strapi as unavailable instead.
    /// </summary>
    public class EligibilityRateTableReader
    {
        // The latest published entry: newest effective date first, one row.
        private const string RatesQuery =
            "/api/eligibility-rates?sort=effectiveDate:desc&pagination[limit]=1";

        private readonly ILogger<EligibilityRateTableReader> _logger;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EligibilityRateTableReader"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="options">The content engine address and this API's token.</param>
        public EligibilityRateTableReader(
            ILogger<EligibilityRateTableReader> logger,
            HttpClient httpClient,
            StrapiReadOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _logger = logger;
            _httpClient = httpClient;
            options.ApplyTo(_httpClient);
        }

        /// <summary>
        /// The compiled table (<see cref="FddRateData"/>) in the shape the content engine
        /// serves: what citizens get when Strapi cannot be read.
        /// </summary>
        /// <returns>The compiled rate table.</returns>
        public static EligibilityRatesModel CompiledTable()
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
                    F = row.TypeF,
                    G = row.TypeG,
                    H = row.TypeH,
                    I = row.TypeI,
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

        /// <summary>
        /// Reads the latest published table.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The table, or null when the content engine holds none, or holds one that is not
        /// in the nine-column shape or is incomplete (the reason is logged).
        /// </returns>
        /// <exception cref="HttpRequestException">The content engine could not be reached or refused the request.</exception>
        /// <exception cref="JsonException">The reply was not JSON.</exception>
        /// <exception cref="KeyNotFoundException">The reply was JSON in an unexpected shape.</exception>
        public async Task<EligibilityRatesModel?> ReadPublishedAsync(CancellationToken cancellationToken)
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(new Uri(RatesQuery, UriKind.Relative), cancellationToken);
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

            string? missingColumn = FindMissingColumn(data[0]);
            if (missingColumn is not null)
            {
                _logger.LogWarning(
                    "The content engine returned an eligibility-rate entry that is not in the nine-column shape ({Reason}); treating it as no usable table.",
                    missingColumn);
                return null;
            }

            EligibilityRatesModel mapped = Map(data[0]);
            if (!IsComplete(mapped, out string reason))
            {
                _logger.LogWarning(
                    "The content engine returned an incomplete eligibility-rate entry ({Reason}); treating it as no usable table.",
                    reason);
                return null;
            }

            return mapped;
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
                    F = row.GetProperty("f").GetDecimal(),
                    G = row.GetProperty("g").GetDecimal(),
                    H = row.GetProperty("h").GetDecimal(),
                    I = row.GetProperty("i").GetDecimal(),
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

        /// <summary>
        /// Names the first rate column missing from an entry, or returns null when
        /// every income row has all nine columns and the asset limits all four. A table
        /// in the old A-E shape is refused here: its letters mean different households.
        /// </summary>
        private static string? FindMissingColumn(JsonElement entry)
        {
            int position = 0;
            foreach (JsonElement row in entry.GetProperty("incomeRows").EnumerateArray())
            {
                position++;
                foreach (string letter in EligibilityRateColumns.Income.Select(column => column.Letter))
                {
                    if (!row.TryGetProperty(letter, out _))
                    {
                        string label = row.TryGetProperty("familySize", out JsonElement size)
                            ? $"family size {size.GetRawText()}"
                            : $"income row {position}";
                        return $"{label} has no column {letter}";
                    }
                }
            }

            JsonElement limits = entry.GetProperty("assetLimits");
            foreach (string letter in EligibilityRateColumns.Asset.Select(column => column.Letter))
            {
                if (!limits.TryGetProperty(letter, out _))
                {
                    return $"asset limits have no column {letter}";
                }
            }

            return null;
        }

        /// <summary>
        /// Confirms a mapped table is safe to serve: a non-empty effective date,
        /// exactly one income row for every family size 1..7, and no negative amounts.
        /// Missing columns are refused before mapping (<see cref="FindMissingColumn"/>),
        /// so this guards the gaps that would otherwise pass silently: missing whole
        /// rows, or a negative amount.
        /// </summary>
        private static bool IsComplete(EligibilityRatesModel rates, out string reason)
        {
            if (string.IsNullOrWhiteSpace(rates.EffectiveDate))
            {
                reason = "missing effective date";
                return false;
            }

            for (int size = 1; size <= EligibilityRateColumns.MaxFamilySize; size++)
            {
                int matches = rates.IncomeRows.Count(row => row.FamilySize == size);
                if (matches != 1)
                {
                    reason = matches == 0
                        ? $"no income row for family size {size}"
                        : $"{matches} income rows for family size {size}";
                    return false;
                }
            }

            // A table stored before Strapi validated rate values could still hold a
            // negative amount, which would yield a nonsensical estimate, so treat it as
            // invalid. Zero is legitimate (the couple columns hold 0 at
            // family size 1), hence the strict < 0 test.
            if (rates.IncomeRows.Any(row => EligibilityRateColumns.Income.Any(column => column.Amount(row) < 0))
                || EligibilityRateColumns.Asset.Any(column => column.Amount(rates.AssetLimits) < 0))
            {
                reason = "negative income or asset amount";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
