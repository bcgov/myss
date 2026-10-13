namespace Myss.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// The only MyssApi path that writes an eligibility rate table: validate, date the
    /// table today in British Columbia, save and publish it, then drop the cached table.
    /// </summary>
    public class EligibilityRatesService : IEligibilityRatesService
    {
        private const string BritishColumbiaTimeZone = "America/Vancouver";

        private readonly IEligibilityRateAdminProvider _adminProvider;
        private readonly IEligibilityRateProvider _rateProvider;
        private readonly TimeProvider _timeProvider;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<EligibilityRatesService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EligibilityRatesService"/> class.
        /// </summary>
        /// <param name="adminProvider">Injected rate-table writer.</param>
        /// <param name="rateProvider">Injected rate-table reader, whose cache a save drops.</param>
        /// <param name="timeProvider">Injected clock.</param>
        /// <param name="currentUser">Injected caller identity, for the audit log line.</param>
        /// <param name="logger">Injected logger.</param>
        public EligibilityRatesService(
            IEligibilityRateAdminProvider adminProvider,
            IEligibilityRateProvider rateProvider,
            TimeProvider timeProvider,
            ICurrentUserAccessor currentUser,
            ILogger<EligibilityRatesService> logger)
        {
            _adminProvider = adminProvider;
            _rateProvider = rateProvider;
            _timeProvider = timeProvider;
            _currentUser = currentUser;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<EligibilityRatesWriteResultModel> SaveAsync(
            SaveEligibilityRatesRequestModel request,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ValidationErrorModel> errors = EligibilityRatesValidator.Validate(request);
            if (errors.Count > 0)
            {
                return EligibilityRatesWriteResultModel.Refused(errors);
            }

            string effectiveDate = TodayInBritishColumbia();
            List<EligibilityRateRowModel> rows = [.. request.IncomeRows.OrderBy(row => row.FamilySize)];
            SavedEligibilityRates saved;
            try
            {
                // Not cancelled with the admin's request: Strapi may already be committing the
                // write. The HTTP client's timeout still bounds it.
                saved = await _adminProvider.SaveTableAsync(
                    effectiveDate, rows, request.AssetLimits, CancellationToken.None);
            }
            catch (StrapiWriteException ex) when (RateRuleRefusal(ex) is string message)
            {
                _logger.LogInformation(
                    "The content engine refused the eligibility rates for {EffectiveDate}: {Message}",
                    effectiveDate,
                    message);
                return EligibilityRatesWriteResultModel.Refused(
                [
                    new ValidationErrorModel
                    {
                        Field = "rates",
                        Keyword = EligibilityRateKeywords.ContentEngineRefused,
                        Message = message,
                    },
                ]);
            }
            catch (ContentEngineUnavailableException)
            {
                // A timeout or an unreadable reply can follow a write Strapi completed, so the
                // cached table is dropped and the attempt is recorded before the 502.
                _rateProvider.InvalidateCache();
                _logger.LogWarning(
                    "Eligibility rates effective {EffectiveDate} by {IdirUsername}: the save failed or its outcome is unknown",
                    effectiveDate,
                    IdirUsername());
                throw;
            }

            _rateProvider.InvalidateCache();

            // Strapi keeps no content history, so this line is the audit record of the change.
            _logger.LogInformation(
                "Eligibility rates effective {EffectiveDate} {Action} by {IdirUsername}",
                effectiveDate,
                saved.Created ? "created" : "updated",
                IdirUsername());
            return EligibilityRatesWriteResultModel.Accepted(saved.Rates);
        }

        /// <summary>
        /// Strapi's message when a 400 is its <c>eligibility-rate</c> lifecycle refusing the
        /// table (an <c>ApplicationError</c> carrying a <c>RATES.</c> keyword), otherwise null:
        /// any other refusal is an upstream fault, and its body is never shown.
        /// </summary>
        private static string? RateRuleRefusal(StrapiWriteException ex)
        {
            if (ex.StatusCode != HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(ex.Body))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(ex.Body);
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("error", out JsonElement error)
                    && error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("name", out JsonElement name)
                    && name.ValueKind == JsonValueKind.String
                    && name.ValueEquals("ApplicationError")
                    && error.TryGetProperty("message", out JsonElement message)
                    && message.ValueKind == JsonValueKind.String
                    && error.TryGetProperty("details", out JsonElement details)
                    && details.ValueKind == JsonValueKind.Object
                    && details.TryGetProperty("keywords", out JsonElement keywords)
                    && keywords.ValueKind == JsonValueKind.Array
                    && keywords.EnumerateArray().Any(keyword =>
                        keyword.ValueKind == JsonValueKind.String
                        && keyword.GetString()!.StartsWith("RATES.", StringComparison.Ordinal)))
                {
                    string? text = message.GetString();
                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private string IdirUsername() => _currentUser.User.IdirUsername ?? "an unknown user";

        private string TodayInBritishColumbia()
        {
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(BritishColumbiaTimeZone);
            DateTimeOffset now = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), zone);
            return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
