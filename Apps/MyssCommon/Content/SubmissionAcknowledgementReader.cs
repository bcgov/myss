namespace Myss.Common.Content
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Reads a form's published confirmation messages from the Strapi content engine,
    /// newest first. It neither caches nor falls back: each API decides what an
    /// unreadable content engine means for it.
    /// </summary>
    public class SubmissionAcknowledgementReader
    {
        private readonly ILogger<SubmissionAcknowledgementReader> _logger;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubmissionAcknowledgementReader"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="options">The content engine address and this API's token.</param>
        public SubmissionAcknowledgementReader(
            ILogger<SubmissionAcknowledgementReader> logger,
            HttpClient httpClient,
            StrapiReadOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _logger = logger;
            _httpClient = httpClient;
            options.ApplyTo(_httpClient);
        }

        /// <summary>
        /// Reads a form's newest published entries. The first is the live message.
        /// </summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="count">The most entries to read.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The entries, newest first; empty when none is published. An entry with a
        /// missing or blank field is left out, and the number left out is logged.
        /// </returns>
        /// <exception cref="HttpRequestException">The content engine could not be reached or refused the request.</exception>
        /// <exception cref="JsonException">The reply was not JSON with a data array.</exception>
        public async Task<IReadOnlyList<SubmissionAcknowledgementEntry>> ReadNewestAsync(
            string formSpecId,
            int count,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(formSpecId);
            ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

            var query = new Uri(
                "/api/submission-acknowledgements"
                + $"?filters[formSpecId][$eq]={Uri.EscapeDataString(formSpecId)}"
                + "&sort=publishedAt:desc"
                + $"&pagination[pageSize]={count.ToString(CultureInfo.InvariantCulture)}",
                UriKind.Relative);

            using HttpResponseMessage response = await _httpClient.GetAsync(query, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "The content engine returned {StatusCode} for the confirmation messages of {FormSpecId}.",
                    (int)response.StatusCode,
                    formSpecId);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document =
                await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("The content engine answered without a data array.");
            }

            var entries = new List<SubmissionAcknowledgementEntry>();
            foreach (JsonElement item in data.EnumerateArray())
            {
                if (ReadEntry(item) is SubmissionAcknowledgementEntry entry)
                {
                    entries.Add(entry);
                }
            }

            int skipped = data.GetArrayLength() - entries.Count;
            if (skipped > 0)
            {
                _logger.LogWarning(
                    "Left out {Skipped} confirmation message entries of {FormSpecId} with a missing or blank field.",
                    skipped,
                    formSpecId);
            }

            return entries;
        }

        /// <summary>Maps one entry as the content engine returns it.</summary>
        /// <param name="item">The entry's JSON object.</param>
        /// <returns>The entry, or null when a field is missing or blank.</returns>
        public static SubmissionAcknowledgementEntry? ReadEntry(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object
                || Text(item, "documentId") is not string id
                || Text(item, "heading") is not string heading
                || Text(item, "title") is not string title
                || Text(item, "body") is not string body
                || Text(item, "changedBy") is not string changedBy
                || !item.TryGetProperty("publishedAt", out JsonElement publishedAt)
                || publishedAt.ValueKind != JsonValueKind.String
                || !publishedAt.TryGetDateTimeOffset(out DateTimeOffset changedAt))
            {
                return null;
            }

            return new SubmissionAcknowledgementEntry
            {
                Id = id,
                Heading = heading,
                Title = title,
                Body = body,
                ChangedBy = changedBy,
                ChangedAt = changedAt,
            };
        }

        private static string? Text(JsonElement item, string name)
        {
            return item.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is string text
                && !string.IsNullOrWhiteSpace(text)
                ? text
                : null;
        }
    }
}
