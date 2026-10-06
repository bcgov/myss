namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Tests for <see cref="StrapiEligibilityRateProvider"/>.
    /// </summary>
    public class StrapiEligibilityRateProviderTests
    {
        // The effective date of the compiled fallback, which matches the seeded table.
        private const string FallbackDate = "2026-10-02";

        // Values deliberately distinct from the compiled fallback, and from each other
        // in rows 2-7, so a passing map test proves the provider read Strapi rather
        // than falling back, and read each letter into its own column.
        private const string RateBody = """
            {
              "data": [
                {
                  "id": 1,
                  "documentId": "rate-doc-1",
                  "effectiveDate": "2099-01-01",
                  "incomeRows": [
                    { "familySize": 1, "a": 0, "b": 111.5, "c": 0, "d": 0, "e": 333.5, "f": 0, "g": 222.5, "h": 0, "i": 0 },
                    { "familySize": 2, "a": 10, "b": 20, "c": 30, "d": 40, "e": 50, "f": 60, "g": 70, "h": 80, "i": 90 },
                    { "familySize": 3, "a": 11, "b": 21, "c": 31, "d": 41, "e": 51, "f": 61, "g": 71, "h": 81, "i": 91 },
                    { "familySize": 4, "a": 12, "b": 22, "c": 32, "d": 42, "e": 52, "f": 62, "g": 72, "h": 82, "i": 92 },
                    { "familySize": 5, "a": 13, "b": 23, "c": 33, "d": 43, "e": 53, "f": 63, "g": 73, "h": 83, "i": 93 },
                    { "familySize": 6, "a": 14, "b": 24, "c": 34, "d": 44, "e": 54, "f": 64, "g": 74, "h": 84, "i": 94 },
                    { "familySize": 7, "a": 15, "b": 25, "c": 35, "d": 45, "e": 55, "f": 65, "g": 75, "h": 85, "i": 95 }
                  ],
                  "assetLimits": { "a": 1, "b": 2, "c": 3, "d": 4 }
                }
              ],
              "meta": { "pagination": { "total": 1 } }
            }
            """;

        private readonly StubHttpHandler _http = new();

        private readonly CapturingLogger _logger = new();

        [Fact]
        public async Task GetRates_MapsThePublishedTable()
        {
            _http.Body = RateBody;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal("2099-01-01", rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
            Assert.Equal(1, rates.IncomeRows[0].FamilySize);
            Assert.Equal(111.5m, rates.IncomeRows[0].B);
            Assert.Equal(333.5m, rates.IncomeRows[0].E);
            Assert.Equal(222.5m, rates.IncomeRows[0].G);
            Assert.Equal(95m, rates.IncomeRows[6].I);
            Assert.Equal(1m, rates.AssetLimits.A);
            Assert.Equal(4m, rates.AssetLimits.D);
        }

        [Fact]
        public async Task GetRates_MapsEveryLetterToItsOwnColumn()
        {
            _http.Body = RateBody;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            EligibilityRateRowModel fs2 = rates.IncomeRows.Single(r => r.FamilySize == 2);
            Assert.Equal(10m, fs2.A);
            Assert.Equal(20m, fs2.B);
            Assert.Equal(30m, fs2.C);
            Assert.Equal(40m, fs2.D);
            Assert.Equal(50m, fs2.E);
            Assert.Equal(60m, fs2.F);
            Assert.Equal(70m, fs2.G);
            Assert.Equal(80m, fs2.H);
            Assert.Equal(90m, fs2.I);
            Assert.Equal(1m, rates.AssetLimits.A);
            Assert.Equal(2m, rates.AssetLimits.B);
            Assert.Equal(3m, rates.AssetLimits.C);
            Assert.Equal(4m, rates.AssetLimits.D);
        }

        [Fact]
        public async Task GetRates_FiveColumnTable_FallsBack()
        {
            // The table in the old A-E shape: complete and valid in that shape, but its
            // C, D and E hold amounts for households that are F, G and H now. Read with
            // the new letters it would mis-estimate without any error.
            _http.Body = """
                {
                  "data": [
                    {
                      "id": 1,
                      "documentId": "rate-doc-five-columns",
                      "effectiveDate": "2023-08-01",
                      "incomeRows": [
                        { "familySize": 1, "a": 0, "b": 1060, "c": 0, "d": 1535.5, "e": 0 },
                        { "familySize": 2, "a": 1650, "b": 1405, "c": 2290.5, "d": 1880.5, "e": 2766 },
                        { "familySize": 3, "a": 1845, "b": 1500, "c": 2485.5, "d": 1975.5, "e": 2961 },
                        { "familySize": 4, "a": 1895, "b": 1550, "c": 2535.5, "d": 2025.5, "e": 3011 },
                        { "familySize": 5, "a": 1945, "b": 1600, "c": 2585.5, "d": 2075.5, "e": 3061 },
                        { "familySize": 6, "a": 1995, "b": 1650, "c": 2635.5, "d": 2125.5, "e": 3111 },
                        { "familySize": 7, "a": 2045, "b": 1700, "c": 2685.5, "d": 2175.5, "e": 3161 }
                      ],
                      "assetLimits": { "a": 5000, "b": 10000, "c": 100000, "d": 200000 }
                    }
                  ],
                  "meta": { "pagination": { "total": 1 } }
                }
                """;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
            EligibilityRateRowModel fs2 = rates.IncomeRows.Single(r => r.FamilySize == 2);
            Assert.Equal(2200.00m, fs2.C);
            Assert.Equal(2290.50m, fs2.F);
            Assert.Contains(_logger.Messages, m => m.Contains("family size 1 has no column f", StringComparison.Ordinal));
        }

        [Fact]
        public async Task GetRates_RowMissingAColumn_FallsBack()
        {
            _http.Body = RateBody.Replace("\"h\": 85, \"i\": 95 }", "\"h\": 85 }", StringComparison.Ordinal);
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
            Assert.Contains(_logger.Messages, m => m.Contains("family size 7 has no column i", StringComparison.Ordinal));
        }

        [Fact]
        public async Task GetRates_AssetLimitsMissingAColumn_FallsBack()
        {
            _http.Body = RateBody.Replace("\"c\": 3, \"d\": 4 }", "\"c\": 3 }", StringComparison.Ordinal);
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
            Assert.Contains(_logger.Messages, m => m.Contains("asset limits have no column d", StringComparison.Ordinal));
        }

        [Fact]
        public async Task GetRates_IncompletePublishedTable_FallsBack()
        {
            // A published entry that is missing family-size rows (here only 1 and 2)
            // must NOT be served: the browser would throw on the absent sizes. The
            // provider treats it as invalid and serves the complete compiled table.
            _http.Body = """
                {
                  "data": [
                    {
                      "id": 1,
                      "documentId": "rate-doc-partial",
                      "effectiveDate": "2099-01-01",
                      "incomeRows": [
                        { "familySize": 1, "a": 0, "b": 111.5, "c": 0, "d": 0, "e": 333.5, "f": 0, "g": 222.5, "h": 0, "i": 0 },
                        { "familySize": 2, "a": 10, "b": 20, "c": 30, "d": 40, "e": 50, "f": 60, "g": 70, "h": 80, "i": 90 }
                      ],
                      "assetLimits": { "a": 1, "b": 2, "c": 3, "d": 4 }
                    }
                  ],
                  "meta": { "pagination": { "total": 1 } }
                }
                """;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
        }

        [Fact]
        public async Task GetRates_QueriesTheLatestPublishedEntry()
        {
            _http.Body = RateBody;
            StrapiEligibilityRateProvider provider = NewProvider();

            await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal("/api/eligibility-rates", _http.LastRequest!.RequestUri!.AbsolutePath);
            string query = _http.LastRequest!.RequestUri!.Query;
            Assert.Contains("sort=effectiveDate:desc", query);
            Assert.Contains("pagination[limit]=1", query);
        }

        [Fact]
        public async Task GetRates_SendsTheConfiguredApiTokenAsABearerHeader()
        {
            _http.Body = RateBody;
            StrapiEligibilityRateProvider provider = NewProvider("tok-rate-123");

            await provider.GetRatesAsync(CancellationToken.None);

            AuthenticationHeaderValue? auth = _http.LastRequest!.Headers.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Equal("tok-rate-123", auth.Parameter);
        }

        [Fact]
        public async Task GetRates_UpstreamError_FallsBackToTheCompiledTable()
        {
            // Strapi down or misconfigured must NOT throw: the public estimator
            // keeps working on the compiled values, which match the seeded table.
            _http.Status = HttpStatusCode.InternalServerError;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
            EligibilityRateRowModel fs1 = rates.IncomeRows.Single(r => r.FamilySize == 1);
            Assert.Equal(1060.00m, fs1.B); // single, not PWD
            Assert.Equal(1360.00m, fs1.E); // single, 65+
            Assert.Equal(1535.50m, fs1.G); // single, PWD
            Assert.Equal(0m, fs1.A);
            Assert.Equal(0m, fs1.I);
            EligibilityRateRowModel fs3 = rates.IncomeRows.Single(r => r.FamilySize == 3);
            Assert.Equal(2961.00m, fs3.H); // couple, both PWD
            Assert.Equal(5000.00m, rates.AssetLimits.A);
            Assert.Equal(200000.00m, rates.AssetLimits.D);
        }

        [Fact]
        public async Task GetRates_UpstreamError_FallbackMapsEveryLetterToItsOwnColumn()
        {
            // Every amount at family size 2 differs, so any swapped letter fails here.
            _http.Status = HttpStatusCode.InternalServerError;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            EligibilityRateRowModel fs2 = rates.IncomeRows.Single(r => r.FamilySize == 2);
            Assert.Equal(1650.00m, fs2.A);
            Assert.Equal(1405.00m, fs2.B);
            Assert.Equal(2200.00m, fs2.C);
            Assert.Equal(1950.00m, fs2.D);
            Assert.Equal(1705.00m, fs2.E);
            Assert.Equal(2290.50m, fs2.F);
            Assert.Equal(1880.50m, fs2.G);
            Assert.Equal(2766.00m, fs2.H);
            Assert.Equal(2590.50m, fs2.I);
            Assert.Equal(5000.00m, rates.AssetLimits.A);
            Assert.Equal(10000.00m, rates.AssetLimits.B);
            Assert.Equal(100000.00m, rates.AssetLimits.C);
            Assert.Equal(200000.00m, rates.AssetLimits.D);
        }

        [Fact]
        public async Task GetRates_EmptyData_FallsBack()
        {
            _http.Body = """{ "data": [], "meta": {} }""";
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
        }

        [Fact]
        public async Task GetRates_NegativeAmount_FallsBack()
        {
            // A complete 7-row table (so it passes the row-count check) but with one
            // negative income amount must NOT be served: it would yield a nonsensical
            // estimate. The provider treats it as invalid and serves the fallback.
            _http.Body = RateBody.Replace("\"b\": 111.5", "\"b\": -111.5", StringComparison.Ordinal);
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
        }

        [Fact]
        public async Task GetRates_NegativeAmountInANewColumn_FallsBack()
        {
            _http.Body = RateBody.Replace("\"i\": 93", "\"i\": -93", StringComparison.Ordinal);
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
        }

        [Fact]
        public async Task GetRates_NegativeAssetLimit_FallsBack()
        {
            _http.Body = RateBody.Replace("\"d\": 4 }", "\"d\": -4 }", StringComparison.Ordinal);
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
        }

        [Fact]
        public async Task GetRates_CachesTheFallback_SecondCallDoesNotReHitStrapi()
        {
            // During a Strapi outage the fallback is cached too, so a burst of
            // requests does not retry Strapi (and wait out the timeout) every time.
            _http.Status = HttpStatusCode.InternalServerError;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel first = await provider.GetRatesAsync(CancellationToken.None);
            EligibilityRatesModel second = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(1, _http.Calls);
            Assert.Equal(FallbackDate, first.EffectiveDate);
            Assert.Equal(FallbackDate, second.EffectiveDate);
        }

        [Fact]
        public async Task InvalidateCache_MakesTheNextReadFetchTheNewTable()
        {
            _http.Body = RateBody;
            StrapiEligibilityRateProvider provider = NewProvider();
            await provider.GetRatesAsync(CancellationToken.None);
            _http.Body = RateBody.Replace("\"2099-01-01\"", "\"2099-02-01\"", StringComparison.Ordinal);

            provider.InvalidateCache();
            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(2, _http.Calls);
            Assert.Equal("2099-02-01", rates.EffectiveDate);
        }

        [Fact]
        public async Task GetRates_CachesTheTable_SecondCallDoesNotReHitStrapi()
        {
            _http.Body = RateBody;
            StrapiEligibilityRateProvider provider = NewProvider();

            await provider.GetRatesAsync(CancellationToken.None);
            await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(1, _http.Calls);
        }

        private StrapiEligibilityRateProvider NewProvider(string? apiToken = null)
        {
            var settings = new Dictionary<string, string?> { ["Strapi:BaseUrl"] = "http://strapi.test" };
            if (apiToken is not null)
            {
                settings["Strapi:ApiToken"] = apiToken;
            }

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            return new StrapiEligibilityRateProvider(
                _logger,
                new HttpClient(_http),
                new MemoryCache(new MemoryCacheOptions()),
                config);
        }

        private sealed class CapturingLogger : ILogger<StrapiEligibilityRateProvider>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => this.Messages.Add(formatter(state, exception));
        }

        private sealed class StubHttpHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            public string Body { get; set; } = "{}";

            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.Calls++;
                this.LastRequest = request;
                var response = new HttpResponseMessage(this.Status)
                {
                    Content = new StringContent(this.Body, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }
        }
    }
}
