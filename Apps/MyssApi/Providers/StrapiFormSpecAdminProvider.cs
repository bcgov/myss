namespace Myss.Api.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Models;

    /// <summary>
    /// Writes form specs to the Strapi content engine over its stock Content
    /// REST API (Strapi 5.51). Built the same way as <see cref="StrapiFormSpecProvider"/>,
    /// but authenticated with the write-scoped <c>Strapi:AdminApiToken</c> and
    /// using the draft/publish calls confirmed in MYSS-209 Step 2:
    /// <list type="bullet">
    ///   <item><description>Create draft: <c>POST /api/form-specs?status=draft</c></description></item>
    ///   <item><description>Update draft: <c>PUT /api/form-specs/{documentId}?status=draft</c></description></item>
    ///   <item><description>Publish:      <c>PUT /api/form-specs/{documentId}?status=published</c></description></item>
    /// </list>
    /// Version sequencing and the immutability of published entries are enforced
    /// by Strapi's lifecycle (<c>form-spec-rules.ts</c>), which runs on this path
    /// and is never bypassed here - the provider only carries the request and
    /// relays any refusal.
    /// </summary>
    public class StrapiFormSpecAdminProvider : IFormSpecAdminProvider
    {
        private const string FormSpecsPath = "/api/form-specs";

        // MyssContent caps the REST maxLimit at 100 (config/api.ts), so results are
        // paged at that size rather than requested in one over-limit call.
        private const int PageSize = 100;

        private readonly ILogger<StrapiFormSpecAdminProvider> _logger;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiFormSpecAdminProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="httpClient">Injected HTTP client.</param>
        /// <param name="configuration">Injected configuration provider.</param>
        public StrapiFormSpecAdminProvider(
            ILogger<StrapiFormSpecAdminProvider> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(configuration.GetValue<string>("Strapi:BaseUrl") ?? "http://localhost:1337");

            // Writes use a SEPARATE, write-scoped token from the read path, so
            // adding an editor never widens the privileges of the endpoints
            // citizens hit. The token is deliberately NOT defaulted: an unset
            // value must fail loudly (a 403 from Strapi) rather than silently
            // do nothing.
            string? adminToken = configuration.GetValue<string>("Strapi:AdminApiToken");
            if (string.IsNullOrWhiteSpace(adminToken))
            {
                _logger.LogWarning(
                    "Strapi:AdminApiToken is not configured. Form-spec writes will be unauthenticated and will "
                    + "fail with 401/403. Set it in appsettings.local.json for local dev, or as the env var "
                    + "Myss_Strapi__AdminApiToken in a container.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", adminToken.Trim());
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<FormSummaryModel>> ListFormsAsync(CancellationToken cancellationToken)
        {
            // Two cheap reads (spec omitted via fields[]): the published copies
            // tell us which versions are live; the draft copies reveal any
            // in-progress (never-published) version. A version is "published"
            // when it appears in the published set.
            const string fields = "&fields[0]=formSpecId&fields[1]=version&fields[2]=title";
            const string sort = "&sort=formSpecId:asc,version:asc";

            IReadOnlyList<Row> published = await GetAllRowsAsync($"{FormSpecsPath}?status=published{fields}{sort}", cancellationToken);
            IReadOnlyList<Row> drafts = await GetAllRowsAsync($"{FormSpecsPath}?status=draft{fields}{sort}", cancellationToken);

            var publishedVersions = published
                .GroupBy(r => r.FormSpecId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.Version).ToHashSet());

            var summaries = new List<FormSummaryModel>();
            foreach (var group in published.Concat(drafts).GroupBy(r => r.FormSpecId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                publishedVersions.TryGetValue(group.Key, out HashSet<int>? live);
                live ??= new HashSet<int>();

                var versions = group
                    .Select(r => r.Version)
                    .Distinct()
                    .OrderBy(v => v)
                    .Select(v => new FormVersionSummaryModel { Version = v, IsPublished = live.Contains(v) })
                    .ToList();

                // Prefer a published row's title, else any draft's, for the display name.
                string? title = group.FirstOrDefault(r => live.Contains(r.Version)).Title
                    ?? group.Select(r => r.Title).FirstOrDefault(t => t is not null);

                summaries.Add(new FormSummaryModel
                {
                    FormSpecId = group.Key,
                    Title = title,
                    Versions = versions,
                });
            }

            return summaries;
        }

        /// <inheritdoc/>
        public async Task<FormSpecModel?> GetDraftAsync(string formSpecId, CancellationToken cancellationToken)
        {
            // The highest-version draft is the editing starting point: an
            // in-progress draft when one exists. When there is none, fall back to
            // the latest published version so opening a live form for editing
            // still returns its current spec (this also makes the method robust to
            // whether Strapi lists a published document's draft copy or not).
            Row? row = await GetFirstRowAsync(DraftLatestQuery(formSpecId), cancellationToken)
                ?? await GetFirstRowAsync(PublishedLatestQuery(formSpecId), cancellationToken);
            return row is { } r ? ToModel(r) : null;
        }

        /// <inheritdoc/>
        public async Task<FormSpecModel> SaveDraftAsync(string formSpecId, JsonElement spec, string? title, CancellationToken cancellationToken)
        {
            Row? inProgress = await FindInProgressDraftAsync(formSpecId, cancellationToken);

            if (inProgress is { } draft)
            {
                // A never-published draft already exists for this form - update it
                // in place so repeated edits don't spawn a new version each save.
                JsonElement saved = await WriteAsync(
                    HttpMethod.Put,
                    $"{FormSpecsPath}/{Uri.EscapeDataString(draft.DocumentId)}?status=draft",
                    BuildData(formSpecId: null, version: null, title: title, spec: spec),
                    cancellationToken);
                return BuildModel(saved, fallbackFormSpecId: formSpecId, fallbackVersion: draft.Version);
            }

            // No in-progress draft: this edit becomes the next version. MyssApi
            // assigns version = latestPublished + 1 (Step 2); Strapi's lifecycle
            // then validates the sequence and rejects a wrong number.
            int nextVersion = await NextVersionAsync(formSpecId, cancellationToken);
            JsonElement created = await WriteAsync(
                HttpMethod.Post,
                $"{FormSpecsPath}?status=draft",
                BuildData(formSpecId: formSpecId, version: nextVersion, title: title, spec: spec),
                cancellationToken);
            return BuildModel(created, fallbackFormSpecId: formSpecId, fallbackVersion: nextVersion);
        }

        /// <inheritdoc/>
        public async Task<int> PublishAsync(string formSpecId, CancellationToken cancellationToken)
        {
            Row? inProgress = await FindInProgressDraftAsync(formSpecId, cancellationToken);
            if (inProgress is not { } draft)
            {
                // Nothing unpublished to release. Surface a clear error rather
                // than a confusing Strapi refusal or a silent no-op.
                throw new NoDraftToPublishException(formSpecId);
            }

            // The version was assigned at save time; the lifecycle validates the
            // sequence and immutability when the document flips to published.
            JsonElement published = await WriteAsync(
                HttpMethod.Put,
                $"{FormSpecsPath}/{Uri.EscapeDataString(draft.DocumentId)}?status=published",
                data: null,
                cancellationToken);

            // A real publish echoes the published entity; an empty object is not
            // evidence the document flipped to published.
            if (published.ValueKind != JsonValueKind.Object || !published.EnumerateObject().Any())
            {
                throw new ContentEngineUnavailableException("The content engine returned an empty publish response.");
            }

            return draft.Version;
        }

        private static string DraftLatestQuery(string formSpecId) =>
            $"{FormSpecsPath}?filters[formSpecId][$eq]={Uri.EscapeDataString(formSpecId)}"
            + "&status=draft&sort=version:desc&pagination[limit]=1";

        private static string PublishedLatestQuery(string formSpecId) =>
            $"{FormSpecsPath}?filters[formSpecId][$eq]={Uri.EscapeDataString(formSpecId)}"
            + "&status=published&sort=version:desc&pagination[limit]=1";

        /// <summary>
        /// Finds the never-published, in-progress draft for a form (its version is
        /// higher than the latest published version), or null when there isn't one.
        /// </summary>
        private async Task<Row?> FindInProgressDraftAsync(string formSpecId, CancellationToken cancellationToken)
        {
            Row? latestDraft = await GetFirstRowAsync(DraftLatestQuery(formSpecId), cancellationToken);
            if (latestDraft is not { } draft)
            {
                return null;
            }

            Row? latestPublished = await GetFirstRowAsync(PublishedLatestQuery(formSpecId), cancellationToken);
            int publishedVersion = latestPublished?.Version ?? 0;
            return draft.Version > publishedVersion ? draft : null;
        }

        private async Task<int> NextVersionAsync(string formSpecId, CancellationToken cancellationToken)
        {
            Row? latestPublished = await GetFirstRowAsync(PublishedLatestQuery(formSpecId), cancellationToken);
            return (latestPublished?.Version ?? 0) + 1;
        }

        private static string BuildData(string? formSpecId, int? version, string? title, JsonElement spec)
        {
            var data = new Dictionary<string, object?>();
            if (formSpecId is not null)
            {
                data["formSpecId"] = formSpecId;
            }

            if (version is not null)
            {
                data["version"] = version.Value;
            }

            if (title is not null)
            {
                data["title"] = title;
            }

            data["spec"] = spec;
            return JsonSerializer.Serialize(new Dictionary<string, object?> { ["data"] = data });
        }

        private async Task<JsonElement> WriteAsync(HttpMethod method, string url, string? data, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, url);
            if (data is not null)
            {
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await SendGuardedAsync(
                () => _httpClient.SendAsync(request, cancellationToken), cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Content engine refused {Method} {Url} with {StatusCode}: {Body}",
                    method.Method,
                    url,
                    (int)response.StatusCode,
                    body);

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning(
                        "The content engine rejected the write as unauthorized. Check that Strapi:AdminApiToken "
                        + "is set and grants create, update and publish on form-spec.");
                }

                // Pass Strapi's own error body up so the service (Step 5) can turn
                // a lifecycle refusal into a readable validation error for the designer.
                throw new StrapiWriteException(response.StatusCode, body);
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            }
            catch (JsonException ex)
            {
                throw new ContentEngineUnavailableException("The content engine returned a malformed response.", ex);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("data", out JsonElement dataElement)
                    || dataElement.ValueKind != JsonValueKind.Object)
                {
                    // A successful write must echo the saved entity as { "data": { ... } };
                    // anything else is a malformed upstream response, not a usable result.
                    throw new ContentEngineUnavailableException(
                        "The content engine returned a malformed write response.");
                }

                return dataElement.Clone();
            }
        }

        /// <summary>
        /// Reads every row for a query by paging at <see cref="PageSize"/> (the content
        /// engine's REST maxLimit). A single over-limit request would be silently capped
        /// and drop forms/versions, so this pages until a short page signals the end.
        /// </summary>
        private async Task<IReadOnlyList<Row>> GetAllRowsAsync(string baseQuery, CancellationToken cancellationToken)
        {
            var all = new List<Row>();
            for (int page = 1; ; page++)
            {
                string query = $"{baseQuery}&pagination[page]={page}&pagination[pageSize]={PageSize}";
                IReadOnlyList<Row> pageRows = await GetRowsAsync(query, cancellationToken);
                all.AddRange(pageRows);

                // A short (or empty) page is the last one. A full page means there may be
                // more; the cap guards against a misconfigured engine never short-paging.
                if (pageRows.Count < PageSize)
                {
                    break;
                }

                if (page >= 1000)
                {
                    throw new ContentEngineUnavailableException(
                        "The content engine returned an unexpectedly large form-spec result set.");
                }
            }

            return all;
        }

        private async Task<IReadOnlyList<Row>> GetRowsAsync(string query, CancellationToken cancellationToken)
        {
            using JsonDocument document = await GetAsync(query, cancellationToken);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array)
            {
                // A legitimate "no results" is data:[] (still an array). A missing or
                // non-array data, or a non-object root, is a malformed upstream response.
                throw new ContentEngineUnavailableException(
                    "The content engine returned a malformed list response.");
            }

            var rows = new List<Row>(data.GetArrayLength());
            try
            {
                foreach (JsonElement entry in data.EnumerateArray())
                {
                    rows.Add(ParseRow(entry));
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException)
            {
                throw new ContentEngineUnavailableException(
                    "The content engine returned a form-spec row in an unexpected shape.", ex);
            }

            return rows;
        }

        private async Task<Row?> GetFirstRowAsync(string query, CancellationToken cancellationToken)
        {
            IReadOnlyList<Row> rows = await GetRowsAsync(query, cancellationToken);
            return rows.Count > 0 ? rows[0] : null;
        }

        private async Task<JsonDocument> GetAsync(string query, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await SendGuardedAsync(
                () => _httpClient.GetAsync(query, cancellationToken), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Content engine returned {StatusCode} for form-spec query {Query}",
                    (int)response.StatusCode,
                    query);

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning(
                        "The content engine rejected the request as unauthorized. Check that Strapi:AdminApiToken "
                        + "is set and grants find and findOne on form-spec.");
                }

                // A non-success read is an upstream/config fault (bad token, outage),
                // not a business outcome - surface it as unavailable, mapped to 502.
                throw new ContentEngineUnavailableException(
                    $"The content engine returned {(int)response.StatusCode} for a read.");
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            }
            catch (JsonException ex)
            {
                throw new ContentEngineUnavailableException("The content engine returned a malformed response.", ex);
            }
        }

        /// <summary>
        /// Runs an HTTP send, converting a transport failure (cannot connect) or a
        /// timeout into <see cref="ContentEngineUnavailableException"/>. A cancellation
        /// requested through the caller's own token is left to propagate as a cancellation,
        /// never disguised as an upstream failure.
        /// </summary>
        /// <param name="send">The send to run.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>The HTTP response.</returns>
        private async Task<HttpResponseMessage> SendGuardedAsync(
            Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
        {
            try
            {
                return await send();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Could not reach the content engine.");
                throw new ContentEngineUnavailableException("The content engine could not be reached.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "The content engine did not respond in time.");
                throw new ContentEngineUnavailableException("The content engine did not respond in time.", ex);
            }
        }

        private static Row ParseRow(JsonElement entry)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new ContentEngineUnavailableException("The content engine returned a non-object form-spec row.");
            }

            bool published = entry.TryGetProperty("publishedAt", out JsonElement pub)
                && pub.ValueKind is not JsonValueKind.Null;

            return new Row(
                DocumentId: ReadOptionalString(entry, "documentId") ?? string.Empty,
                FormSpecId: ReadRequiredString(entry, "formSpecId", fallback: null),
                Version: ReadRequiredVersion(entry, fallback: null),
                Title: ReadOptionalString(entry, "title"),
                Spec: entry.TryGetProperty("spec", out JsonElement spec) ? spec.Clone() : null,
                Published: published);
        }

        /// <summary>
        /// Builds the editable model for a loaded draft. A draft query returns the full
        /// entity, so the spec must be an object; a missing or scalar spec is a malformed
        /// upstream response, not an editable form.
        /// </summary>
        private static FormSpecModel ToModel(Row row)
        {
            if (row.Spec is not { ValueKind: JsonValueKind.Object } spec)
            {
                throw new ContentEngineUnavailableException(
                    "The content engine returned a form-spec without an object-valued spec.");
            }

            return new FormSpecModel
            {
                FormSpecId = row.FormSpecId,
                Version = row.Version,
                Title = row.Title,
                Spec = spec,
            };
        }

        /// <summary>
        /// Builds the model echoed by a successful write. The entity must carry a valid
        /// object-valued spec (so an empty <c>{ }</c> is rejected as unproven); id and
        /// version fall back to the values we sent only when the response OMITS them - a
        /// present-but-malformed id or version is rejected, not masked.
        /// </summary>
        private static FormSpecModel BuildModel(JsonElement entity, string? fallbackFormSpecId, int? fallbackVersion)
        {
            if (entity.ValueKind != JsonValueKind.Object)
            {
                throw new ContentEngineUnavailableException("The content engine returned a non-object form-spec entity.");
            }

            return new FormSpecModel
            {
                FormSpecId = ReadRequiredString(entity, "formSpecId", fallbackFormSpecId),
                Version = ReadRequiredVersion(entity, fallbackVersion),
                Title = ReadOptionalString(entity, "title"),
                Spec = ReadRequiredObject(entity, "spec"),
            };
        }

        /// <summary>
        /// Reads a required non-empty string field. The fallback is used only when the
        /// field is ABSENT; a present value that is not a non-empty string is a malformed
        /// response and is rejected rather than masked by the fallback.
        /// </summary>
        private static string ReadRequiredString(JsonElement entity, string name, string? fallback)
        {
            if (!entity.TryGetProperty(name, out JsonElement el))
            {
                return string.IsNullOrEmpty(fallback)
                    ? throw new ContentEngineUnavailableException($"The content engine response is missing '{name}'.")
                    : fallback;
            }

            return el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 } value
                ? value
                : throw new ContentEngineUnavailableException($"The content engine returned an invalid '{name}'.");
        }

        /// <summary>
        /// Reads a required positive integer <c>version</c> (the schema minimum is 1). The
        /// fallback is used only when the field is ABSENT; a present value that is the wrong
        /// type, non-integral, or below 1 is a malformed response and is rejected.
        /// </summary>
        private static int ReadRequiredVersion(JsonElement entity, int? fallback)
        {
            if (!entity.TryGetProperty("version", out JsonElement el))
            {
                return fallback ?? throw new ContentEngineUnavailableException(
                    "The content engine response is missing a 'version'.");
            }

            return el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int value) && value >= 1
                ? value
                : throw new ContentEngineUnavailableException(
                    "The content engine returned a 'version' that is not a positive integer.");
        }

        /// <summary>Reads an optional string field; a present value of any other type is a malformed response.</summary>
        private static string? ReadOptionalString(JsonElement entity, string name)
        {
            if (!entity.TryGetProperty(name, out JsonElement el) || el.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : throw new ContentEngineUnavailableException(
                    $"The content engine returned a '{name}' of an unexpected type.");
        }

        /// <summary>Reads a required object-valued field (a clone), else a malformed response.</summary>
        private static JsonElement ReadRequiredObject(JsonElement entity, string name)
        {
            if (entity.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.Object)
            {
                return el.Clone();
            }

            throw new ContentEngineUnavailableException(
                $"The content engine response is missing an object-valued '{name}'.");
        }

        /// <summary>
        /// A parsed Strapi form-spec row. Spec is null on list queries that omit it.
        /// </summary>
        private readonly record struct Row(
            string DocumentId,
            string FormSpecId,
            int Version,
            string? Title,
            JsonElement? Spec,
            bool Published);
    }

    /// <summary>
    /// Thrown when the content engine refuses a form-spec write. Carries Strapi's
    /// HTTP status and raw error body so the caller can surface the reason (for
    /// example, a lifecycle validation refusal) rather than a generic failure.
    /// </summary>
    public class StrapiWriteException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiWriteException"/> class.
        /// </summary>
        /// <param name="statusCode">The HTTP status Strapi returned.</param>
        /// <param name="body">The raw response body Strapi returned.</param>
        public StrapiWriteException(HttpStatusCode statusCode, string? body)
            : base($"Strapi refused the form-spec write with {(int)statusCode}.")
        {
            this.StatusCode = statusCode;
            this.Body = body;
        }

        /// <summary>
        /// Gets the HTTP status the content engine returned.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the raw response body the content engine returned, if any.
        /// </summary>
        public string? Body { get; }
    }

    /// <summary>
    /// Thrown when the content engine cannot be reached, times out, returns a
    /// non-success status on a read, or returns a malformed response - an
    /// infrastructure/configuration failure rather than a business refusal. The API
    /// maps it to 502 with a generic message; the detail is logged, not returned.
    /// </summary>
    public class ContentEngineUnavailableException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="ContentEngineUnavailableException"/> class.</summary>
        /// <param name="message">A description of the failure.</param>
        public ContentEngineUnavailableException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ContentEngineUnavailableException"/> class.</summary>
        /// <param name="message">A description of the failure.</param>
        /// <param name="innerException">The underlying transport or parse failure.</param>
        public ContentEngineUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown by the admin provider when a publish is requested but the form has no
    /// in-progress (never-published) draft to release. A distinct type so the service
    /// catches ONLY this case, never an unrelated <see cref="System.InvalidOperationException"/>
    /// (for example one raised while parsing a malformed upstream response).
    /// </summary>
    public class NoDraftToPublishException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="NoDraftToPublishException"/> class.</summary>
        /// <param name="formSpecId">The form that had no in-progress draft.</param>
        public NoDraftToPublishException(string formSpecId)
            : base($"No in-progress draft to publish for form '{formSpecId}'. Save a draft before publishing.")
        {
            this.FormSpecId = formSpecId;
        }

        /// <summary>Gets the form that had no in-progress draft.</summary>
        public string FormSpecId { get; }
    }
}
