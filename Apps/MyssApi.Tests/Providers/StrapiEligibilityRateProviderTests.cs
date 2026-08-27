namespace Myss.Api.Tests.Providers
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Tests for <see cref="StrapiEligibilityRateProvider"/>.
    /// </summary>
    public class StrapiEligibilityRateProviderTests
    {
        // Values deliberately distinct from the compiled fallback, so a passing
        // map test proves the provider read Strapi rather than falling back.
        private const string RateBody = """
            {
              "data": [
                {
                  "id": 1,
                  "documentId": "rate-doc-1",
                  "effectiveDate": "2099-01-01",
                  "incomeRows": [
                    { "familySize": 1, "a": 0, "b": 111.5, "c": 0, "d": 222.5, "e": 0 },
                    { "familySize": 2, "a": 10, "b": 20, "c": 30, "d": 40, "e": 50 },
                    { "familySize": 3, "a": 11, "b": 21, "c": 31, "d": 41, "e": 51 },
                    { "familySize": 4, "a": 12, "b": 22, "c": 32, "d": 42, "e": 52 },
                    { "familySize": 5, "a": 13, "b": 23, "c": 33, "d": 43, "e": 53 },
                    { "familySize": 6, "a": 14, "b": 24, "c": 34, "d": 44, "e": 54 },
                    { "familySize": 7, "a": 15, "b": 25, "c": 35, "d": 45, "e": 55 }
                  ],
                  "assetLimits": { "a": 1, "b": 2, "c": 3, "d": 4 }
                }
              ],
              "meta": { "pagination": { "total": 1 } }
            }
            """;

        private readonly StubHttpHandler _http = new();

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
            Assert.Equal(222.5m, rates.IncomeRows[0].D);
            Assert.Equal(50m, rates.IncomeRows[1].E);
            Assert.Equal(1m, rates.AssetLimits.A);
            Assert.Equal(4m, rates.AssetLimits.D);
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
                        { "familySize": 1, "a": 0, "b": 111.5, "c": 0, "d": 222.5, "e": 0 },
                        { "familySize": 2, "a": 10, "b": 20, "c": 30, "d": 40, "e": 50 }
                      ],
                      "assetLimits": { "a": 1, "b": 2, "c": 3, "d": 4 }
                    }
                  ],
                  "meta": { "pagination": { "total": 1 } }
                }
                """;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal("2023-08-01", rates.EffectiveDate);
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
        public async Task GetRates_UpstreamError_FallsBackToTheCompiledMyss25Table()
        {
            // Strapi down or misconfigured must NOT throw: the public estimator
            // keeps working on the compiled MYSS-25 values.
            _http.Status = HttpStatusCode.InternalServerError;
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal("2023-08-01", rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
            EligibilityRateRowModel fs1 = rates.IncomeRows.Single(r => r.FamilySize == 1);
            Assert.Equal(1060.00m, fs1.B); // single, not PWD
            Assert.Equal(1535.50m, fs1.D); // single, PWD
            Assert.Equal(0m, fs1.A);
            EligibilityRateRowModel fs3 = rates.IncomeRows.Single(r => r.FamilySize == 3);
            Assert.Equal(2961.00m, fs3.E); // couple, both PWD (MYSS-25)
            Assert.Equal(5000.00m, rates.AssetLimits.A);
            Assert.Equal(200000.00m, rates.AssetLimits.D);
        }

        [Fact]
        public async Task GetRates_EmptyData_FallsBack()
        {
            _http.Body = """{ "data": [], "meta": {} }""";
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal("2023-08-01", rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
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
                NullLogger<StrapiEligibilityRateProvider>.Instance,
                new HttpClient(_http),
                new MemoryCache(new MemoryCacheOptions()),
                config);
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
