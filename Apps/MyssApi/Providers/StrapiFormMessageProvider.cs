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
    /// Reads a form message's live texts through <see cref="FormMessageReader"/>
    /// and caches what it found per form and message key.
    /// </summary>
    public class StrapiFormMessageProvider : IFormMessageProvider
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

        private readonly ILogger<StrapiFormMessageProvider> _logger;
        private readonly FormMessageReader _reader;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiFormMessageProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="reader">Injected content engine reader.</param>
        /// <param name="cache">Injected in-memory cache.</param>
        public StrapiFormMessageProvider(
            ILogger<StrapiFormMessageProvider> logger,
            FormMessageReader reader,
            IMemoryCache cache)
        {
            _logger = logger;
            _reader = reader;
            _cache = cache;
        }

        /// <inheritdoc/>
        public async Task<FormMessageLookup> GetLiveAsync(
            string formSpecId,
            string messageKey,
            CancellationToken cancellationToken)
        {
            string cacheKey = $"form-message:{formSpecId}:{messageKey}";
            if (_cache.TryGetValue(cacheKey, out FormMessageLookup? cached) && cached is not null)
            {
                return cached;
            }

            FormMessageLookup lookup;
            TimeSpan duration;
            try
            {
                IReadOnlyList<FormMessageEntry> entries =
                    await _reader.ReadNewestAsync(formSpecId, messageKey, EntriesRead, cancellationToken);
                lookup = entries.Count == 0
                    ? FormMessageLookup.None
                    : FormMessageLookup.Found(new FormMessageModel
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
                    "Could not read form message {MessageKey} for {FormSpecId} from the content engine; the page shows its built-in message.",
                    messageKey,
                    formSpecId);
                lookup = FormMessageLookup.Unavailable;
                duration = UnavailableCacheDuration;
            }

            _cache.Set(cacheKey, lookup, duration);
            return lookup;
        }
    }
}
