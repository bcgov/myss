namespace Myss.Api.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;
    using Myss.Common.Content;

    /// <summary>
    /// Reads a form's live confirmation message through
    /// <see cref="SubmissionAcknowledgementReader"/> and caches what it found per form.
    /// </summary>
    public class StrapiSubmissionAcknowledgementProvider : ISubmissionAcknowledgementProvider
    {
        // The live message is the newest entry the reader accepts, as in the admin editor;
        // a few are read in case the newest is left out for a blank field.
        private const int EntriesRead = 5;

        // Messages are saved through MyssAdminApi, another process that cannot clear
        // this cache, so a saved message reaches applicants within this window.
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

        // Short, so a Strapi outage does not make every page load wait out the HTTP
        // client's timeout, and a recovered Strapi is read again soon.
        private static readonly TimeSpan UnavailableCacheDuration = TimeSpan.FromSeconds(30);

        private readonly ILogger<StrapiSubmissionAcknowledgementProvider> _logger;
        private readonly SubmissionAcknowledgementReader _reader;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiSubmissionAcknowledgementProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="reader">Injected content engine reader.</param>
        /// <param name="cache">Injected in-memory cache.</param>
        public StrapiSubmissionAcknowledgementProvider(
            ILogger<StrapiSubmissionAcknowledgementProvider> logger,
            SubmissionAcknowledgementReader reader,
            IMemoryCache cache)
        {
            _logger = logger;
            _reader = reader;
            _cache = cache;
        }

        /// <inheritdoc/>
        public async Task<SubmissionAcknowledgementLookup> GetLiveAsync(
            string formSpecId,
            CancellationToken cancellationToken)
        {
            string cacheKey = "submission-acknowledgement:" + formSpecId;
            if (_cache.TryGetValue(cacheKey, out SubmissionAcknowledgementLookup? cached) && cached is not null)
            {
                return cached;
            }

            SubmissionAcknowledgementLookup lookup;
            TimeSpan duration;
            try
            {
                IReadOnlyList<SubmissionAcknowledgementEntry> entries =
                    await _reader.ReadNewestAsync(formSpecId, EntriesRead, cancellationToken);
                lookup = entries.Count == 0
                    ? SubmissionAcknowledgementLookup.None
                    : SubmissionAcknowledgementLookup.Found(new SubmissionAcknowledgementModel
                    {
                        Heading = entries[0].Heading,
                        Title = entries[0].Title,
                        Body = entries[0].Body,
                    });
                duration = CacheDuration;
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Could not read the confirmation message for {FormSpecId} from the content engine; the page shows its built-in message.",
                    formSpecId);
                lookup = SubmissionAcknowledgementLookup.Unavailable;
                duration = UnavailableCacheDuration;
            }

            _cache.Set(cacheKey, lookup, duration);
            return lookup;
        }
    }
}
