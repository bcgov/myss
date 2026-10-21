namespace Myss.AdminApi.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Myss.Common.Content;

    /// <summary>
    /// Reads the eligibility rate table for the rates editor, through
    /// <see cref="EligibilityRateTableReader"/>. Unlike MyssApi's citizen read it is not
    /// cached, so the editor shows a save as soon as it is made, and it does NOT fall
    /// back when Strapi cannot be read: it throws
    /// <see cref="ContentEngineUnavailableException"/> (a 502). The citizen fallback
    /// keeps the estimator working; in the editor the same fallback would offer the
    /// compiled table for editing, and one edited cell saved over it would publish the
    /// stale compiled values in place of the live table. When Strapi answers but holds
    /// no usable table (none published yet, or one not in the nine-column shape), the
    /// compiled table is returned: that is what citizens are being served, so it is the
    /// right starting point to edit.
    /// </summary>
    public class StrapiEligibilityRateProvider : IEligibilityRateProvider
    {
        private readonly ILogger<StrapiEligibilityRateProvider> _logger;
        private readonly EligibilityRateTableReader _reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiEligibilityRateProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="reader">Injected content engine reader.</param>
        public StrapiEligibilityRateProvider(
            ILogger<StrapiEligibilityRateProvider> logger,
            EligibilityRateTableReader reader)
        {
            _logger = logger;
            _reader = reader;
        }

        /// <inheritdoc/>
        public async Task<EligibilityRatesModel> GetRatesAsync(CancellationToken cancellationToken)
        {
            EligibilityRatesModel? rates;
            try
            {
                rates = await _reader.ReadPublishedAsync(cancellationToken);
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throw new ContentEngineUnavailableException(
                    "Could not read the eligibility rate table from the content engine.", ex);
            }

            if (rates is not null)
            {
                return rates;
            }

            _logger.LogWarning(
                "The content engine holds no usable eligibility-rate entry; the editor starts from the compiled table.");
            return EligibilityRateTableReader.CompiledTable();
        }
    }
}
