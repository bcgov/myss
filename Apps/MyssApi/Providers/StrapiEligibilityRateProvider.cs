namespace Myss.Api.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Myss.Common.Content;

    /// <summary>
    /// Serves the eligibility rate table to the public estimator: the latest
    /// published table from the Strapi content engine (read by
    /// <see cref="EligibilityRateTableReader"/>), cached. When Strapi cannot be read,
    /// or its table is not in the nine-column shape, the compiled table
    /// (<see cref="EligibilityRateTableReader.CompiledTable"/>) is returned so the
    /// estimator keeps working.
    /// </summary>
    public class StrapiEligibilityRateProvider : IEligibilityRateProvider
    {
        private const string CacheKey = "eligibility-rate-table";

        // The admin rates editor saves through MyssAdminApi, another process that
        // cannot clear this cache, so a saved table reaches citizens within this window.
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        // The compiled fallback is cached under a short TTL so a Strapi outage does
        // not make every anonymous /rates request retry Strapi (and wait out the
        // HttpClient timeout). Kept much shorter than the success TTL so a recovered
        // Strapi is picked up promptly.
        private static readonly TimeSpan FallbackCacheDuration = TimeSpan.FromSeconds(30);

        private readonly ILogger<StrapiEligibilityRateProvider> _logger;
        private readonly EligibilityRateTableReader _reader;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiEligibilityRateProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="reader">Injected content engine reader.</param>
        /// <param name="cache">Injected in-memory cache.</param>
        public StrapiEligibilityRateProvider(
            ILogger<StrapiEligibilityRateProvider> logger,
            EligibilityRateTableReader reader,
            IMemoryCache cache)
        {
            _logger = logger;
            _reader = reader;
            _cache = cache;
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
                EligibilityRatesModel? rates = await _reader.ReadPublishedAsync(cancellationToken);
                if (rates is not null)
                {
                    _cache.Set(CacheKey, rates, CacheDuration);
                    return rates;
                }

                _logger.LogWarning(
                    "The content engine returned no usable eligibility-rate entry; using the compiled fallback.");
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Could not read the eligibility rate table from the content engine; using the compiled fallback.");
            }

            EligibilityRatesModel fallback = EligibilityRateTableReader.CompiledTable();
            _cache.Set(CacheKey, fallback, FallbackCacheDuration);
            return fallback;
        }
    }
}
