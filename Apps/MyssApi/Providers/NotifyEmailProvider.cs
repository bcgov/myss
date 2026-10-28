namespace Myss.Api.Providers
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Myss.Api.Configuration.Models;

    /// <summary>
    /// Sends email through BC Gov Notify's simple send,
    /// <c>POST /api/v1/notifysimple/email</c>, with the tenant's key in
    /// <c>X-API-KEY</c> (MYSS-36). Notify answers 202 with a <c>notifyId</c> once
    /// it has accepted the message; delivery happens afterwards. Read from the
    /// published OpenAPI document at <c>/api/docs-json</c> on 2026-10-20.
    /// </summary>
    /// <remarks>
    /// No retry: a send is not idempotent from here. Notify drops an identical
    /// request inside its deduplication window, but a citizen can simply ask again.
    /// </remarks>
    public class NotifyEmailProvider : IEmailProvider
    {
        /// <summary>The send route, relative to <see cref="NotifyConfig.BaseUrl"/>.</summary>
        public const string SendEmailPath = "api/v1/notifysimple/email";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly ILogger<NotifyEmailProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly NotifyConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotifyEmailProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        /// <param name="httpClient">Injected HTTP client; its timeout is set at registration.</param>
        /// <param name="config">The Notify settings.</param>
        public NotifyEmailProvider(
            ILogger<NotifyEmailProvider> logger,
            HttpClient httpClient,
            IOptions<NotifyConfig> config)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = config.Value;
        }

        /// <inheritdoc/>
        public async Task<string> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (!_config.IsConfigured)
            {
                throw new EmailNotSentException(
                    "Notify is not configured (an https BaseUrl and an ApiKey are required). "
                    + "Locally: set Notify:ApiKey in appsettings.local.json. "
                    + "Deployed: set Myss_Notify__BaseUrl and Myss_Notify__ApiKey from the secret.");
            }

            var payload = new NotifyEmailRequest(
                new NotifyRecipients([message.To]),
                new NotifyInlineContent(message.Subject, message.MarkdownBody, "markdown"));
            var url = new Uri($"{_config.BaseUrl!.AbsoluteUri.TrimEnd('/')}/{SendEmailPath}");
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };
            request.Headers.Add("X-API-KEY", _config.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new EmailNotSentException("Notify could not be reached.", ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new EmailNotSentException("Notify did not answer in time.", ex);
            }

            using (response)
            {
                // The body of a refusal is not logged: Notify's message can
                // echo the recipient address back.
                if (response.StatusCode != HttpStatusCode.Accepted && response.StatusCode != HttpStatusCode.OK)
                {
                    // 400 and 422 are Notify's answer to a request it will not
                    // take, which for a plain send is the address; 401, 403 and
                    // 429 are our key or our rate, and 5xx is Notify itself.
                    throw new EmailNotSentException(
                        $"Notify refused the message ({(int)response.StatusCode}).")
                    {
                        RecipientRejected = response.StatusCode
                            is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
                    };
                }

                string? notifyId;
                string? status;
                bool duplicate;
                try
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using JsonDocument body = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    JsonElement root = body.RootElement;
                    notifyId = ReadString(root, "notifyId");
                    status = ReadString(root, "status");
                    duplicate = root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("duplicate", out JsonElement d)
                        && d.ValueKind == JsonValueKind.True;
                }
                catch (JsonException ex)
                {
                    throw new EmailNotSentException("Notify accepted the message but its answer could not be read.", ex);
                }

                if (string.IsNullOrWhiteSpace(notifyId))
                {
                    throw new EmailNotSentException("Notify's answer carried no notifyId.");
                }

                _logger.LogInformation(
                    "Notify accepted email {NotifyId} with status {NotifyStatus}{Duplicate}",
                    notifyId,
                    status,
                    duplicate ? " (duplicate of an earlier request)" : string.Empty);
                return notifyId;
            }
        }

        private static string? ReadString(JsonElement root, string name) =>
            root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

        private sealed record NotifyEmailRequest(NotifyRecipients Recipients, NotifyInlineContent Content);

        private sealed record NotifyRecipients(string[] To);

        private sealed record NotifyInlineContent(string Subject, string Body, string BodyType);

    }
}
