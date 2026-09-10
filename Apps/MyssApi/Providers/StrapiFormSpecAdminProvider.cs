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
            const string paging = "&sort=formSpecId:asc,version:asc&pagination[limit]=1000";

            IReadOnlyList<Row> published = await GetRowsAsync($"{FormSpecsPath}?status=published{fields}{paging}", cancellationToken);
            IReadOnlyList<Row> drafts = await GetRowsAsync($"{FormSpecsPath}?status=draft{fields}{paging}", cancellationToken);

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
                return ToModel(saved, fallbackFormSpecId: formSpecId, fallbackVersion: draft.Version);
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
            return ToModel(created, fallbackFormSpecId: formSpecId, fallbackVersion: nextVersion);
        }

        /// <inheritdoc/>
        public async Task<int> PublishAsync(string formSpecId, CancellationToken cancellationToken)
        {
            Row? inProgress = await FindInProgressDraftAsync(formSpecId, cancellationToken);
            if (inProgress is not { } draft)
            {
                // Nothing unpublished to release. Surface a clear error rather
                // than a confusing Strapi refusal or a silent no-op.
                throw new InvalidOperationException(
                    $"No in-progress draft to publish for form '{formSpecId}'. Save a draft before publishing.");
            }

            // The version was assigned at save time; the lifecycle validates the
            // sequence and immutability when the document flips to published.
            await WriteAsync(
                HttpMethod.Put,
                $"{FormSpecsPath}/{Uri.EscapeDataString(draft.DocumentId)}?status=published",
                data: null,
                cancellationToken);
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

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
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

            using JsonDocument document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            return document.RootElement.TryGetProperty("data", out JsonElement dataElement)
                ? dataElement.Clone()
                : document.RootElement.Clone();
        }

        private async Task<IReadOnlyList<Row>> GetRowsAsync(string query, CancellationToken cancellationToken)
        {
            using JsonDocument document = await GetAsync(query, cancellationToken);
            if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<Row>();
            }

            var rows = new List<Row>(data.GetArrayLength());
            foreach (JsonElement entry in data.EnumerateArray())
            {
                rows.Add(ParseRow(entry));
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
            using HttpResponseMessage response = await _httpClient.GetAsync(query, cancellationToken);
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
            }

            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }

        private static Row ParseRow(JsonElement entry)
        {
            JsonElement? spec = entry.TryGetProperty("spec", out JsonElement s) ? s.Clone() : null;
            bool published = entry.TryGetProperty("publishedAt", out JsonElement pub)
                && pub.ValueKind is not JsonValueKind.Null;

            return new Row(
                DocumentId: entry.TryGetProperty("documentId", out JsonElement doc) ? doc.GetString() ?? string.Empty : string.Empty,
                FormSpecId: entry.GetProperty("formSpecId").GetString()!,
                Version: entry.GetProperty("version").GetInt32(),
                Title: entry.TryGetProperty("title", out JsonElement title) ? title.GetString() : null,
                Spec: spec,
                Published: published);
        }

        private static FormSpecModel ToModel(Row row) => new()
        {
            FormSpecId = row.FormSpecId,
            Version = row.Version,
            Title = row.Title,
            Spec = row.Spec ?? EmptySpec(),
        };

        private static FormSpecModel ToModel(JsonElement entry, string fallbackFormSpecId, int fallbackVersion) => new()
        {
            FormSpecId = entry.TryGetProperty("formSpecId", out JsonElement id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()!
                : fallbackFormSpecId,
            Version = entry.TryGetProperty("version", out JsonElement v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : fallbackVersion,
            Title = entry.TryGetProperty("title", out JsonElement t) ? t.GetString() : null,
            Spec = entry.TryGetProperty("spec", out JsonElement s) ? s.Clone() : EmptySpec(),
        };

        private static JsonElement EmptySpec()
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
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
}
