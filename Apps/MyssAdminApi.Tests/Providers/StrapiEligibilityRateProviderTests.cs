namespace Myss.AdminApi.Tests.Providers
{
    using System.Net;
    using System.Text;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.Common.Content;

    /// <summary>
    /// Tests for <see cref="StrapiEligibilityRateProvider"/>: the editor's read is not
    /// cached and reports an unreadable content engine instead of falling back. Reading
    /// and checking the table itself is tested in MyssCommon.Tests
    /// (EligibilityRateTableReaderTests).
    /// </summary>
    public sealed class StrapiEligibilityRateProviderTests : IDisposable
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

        /// <inheritdoc/>
        public void Dispose() => _http.Dispose();

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

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task GetRates_UpstreamError_Throws_NeverOffersTheCompiledTable(HttpStatusCode status)
        {
            // Unlike the citizen read, the editor must not fall back when Strapi cannot
            // be read (down, or the token missing or under-scoped): an admin would edit
            // the compiled table and one save would publish its stale values.
            _http.Status = status;
            StrapiEligibilityRateProvider provider = NewProvider();

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => provider.GetRatesAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetRates_UnparseableReply_Throws()
        {
            _http.Body = "<html>maintenance</html>";
            StrapiEligibilityRateProvider provider = NewProvider();

            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => provider.GetRatesAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetRates_NoPublishedTable_StartsFromTheCompiledTable()
        {
            // Strapi answers but holds no table yet: citizens are served the compiled
            // values, which match the seeded table, so the editor starts from them.
            _http.Body = """{ "data": [], "meta": {} }""";
            StrapiEligibilityRateProvider provider = NewProvider();

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, rates.EffectiveDate);
            Assert.Equal(7, rates.IncomeRows.Count);
        }

        [Fact]
        public async Task GetRates_IsNotCached_ASecondReadSeesTheNewTable()
        {
            // The editor must show a save straight away, so every read goes to Strapi.
            _http.Body = RateBody;
            StrapiEligibilityRateProvider provider = NewProvider();
            await provider.GetRatesAsync(CancellationToken.None);
            _http.Body = RateBody.Replace("\"2099-01-01\"", "\"2099-02-01\"", StringComparison.Ordinal);

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(2, _http.Calls);
            Assert.Equal("2099-02-01", rates.EffectiveDate);
        }

        [Fact]
        public async Task GetRates_DoesNotCacheTheFallback_TheNextReadRetriesStrapi()
        {
            _http.Body = """{ "data": [], "meta": {} }""";
            StrapiEligibilityRateProvider provider = NewProvider();
            EligibilityRatesModel first = await provider.GetRatesAsync(CancellationToken.None);
            _http.Body = RateBody;

            EligibilityRatesModel second = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal(FallbackDate, first.EffectiveDate);
            Assert.Equal("2099-01-01", second.EffectiveDate);
        }

        [Fact]
        public async Task GetRates_AfterAFailedRead_TheNextReadRetriesStrapi()
        {
            _http.Status = HttpStatusCode.InternalServerError;
            StrapiEligibilityRateProvider provider = NewProvider();
            await Assert.ThrowsAsync<ContentEngineUnavailableException>(
                () => provider.GetRatesAsync(CancellationToken.None));
            _http.Status = HttpStatusCode.OK;
            _http.Body = RateBody;

            EligibilityRatesModel rates = await provider.GetRatesAsync(CancellationToken.None);

            Assert.Equal("2099-01-01", rates.EffectiveDate);
        }

        [Fact]
        public void ReadsWithTheAdminToken_NotTheReadOnlyOne()
        {
            // This API holds only the write-scoped token; a read-only token left in its
            // configuration must not be what its readers authenticate with.
            using WebApplicationFactory<Startup> factory = new WebApplicationFactory<Startup>()
                .WithWebHostBuilder(builder => builder
                    .UseSetting("Strapi:AdminApiToken", "admin-token")
                    .UseSetting("Strapi:ApiToken", "read-only-token"));

            StrapiReadOptions options = factory.Services.GetRequiredService<StrapiReadOptions>();

            Assert.Equal("admin-token", options.ApiToken);
            Assert.Equal("Strapi:AdminApiToken", options.TokenSettingName);
        }

        private StrapiEligibilityRateProvider NewProvider()
        {
            var reader = new EligibilityRateTableReader(
                NullLogger<EligibilityRateTableReader>.Instance,
                new HttpClient(_http),
                new StrapiReadOptions { BaseAddress = new Uri("http://strapi.test"), TokenSettingName = "Strapi:AdminApiToken" });

            return new StrapiEligibilityRateProvider(NullLogger<StrapiEligibilityRateProvider>.Instance, reader);
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
