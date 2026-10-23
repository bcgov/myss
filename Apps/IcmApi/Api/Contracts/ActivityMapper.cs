namespace Icm.Api.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Icm.Api.Models;

    /// <summary>
    /// Converts between Siebel's activity and activity plan rows and the published
    /// <see cref="Activity"/> and <see cref="ActivityPlan"/>, turns an
    /// <see cref="ActivityInput"/> into the row to create, and turns the queries into
    /// Siebel search expressions — the only place they are built, so the only place a
    /// value has to be vetted.
    /// </summary>
    internal static class ActivityMapper
    {
        /// <summary>The visibility mode every activity read uses unless told otherwise (MEASURED: the one that sees bus pass SRs and their children).</summary>
        public const string DefaultViewMode = "Organization";

        /// <summary>The rows a service request's child read asks for per page; the repository follows <c>lastpage</c>.</summary>
        public const int ChildPageSize = 100;

        /// <summary>
        /// The <c>Id</c> every create sends: the literal ICM's REST API guidance prescribes
        /// as a new activity's user key, which Siebel replaces with the row id it allocates
        /// (MEASURED 2026-09-29, <c>1-53MU3Y4</c>). Never a caller's: the create is an
        /// upsert, so a real row id would update that activity.
        /// </summary>
        public const string CreatePlaceholderId = "NULL";

        /// <summary>The <c>Category</c> a create sends when the caller gave none: what every bus pass step carries.</summary>
        public const string DefaultCategory = "General";

        /// <summary>The <c>Display</c> a create sends when the caller gave none: what every bus pass step carries.</summary>
        public const string DefaultDisplay = "Calendar and Activities";

        /// <summary>Converts a published activity search to the wire query.</summary>
        /// <param name="query">The search.</param>
        /// <returns>The wire query.</returns>
        /// <exception cref="ArgumentException">No criterion, or a blank or unsafe one; the message names the property, never the value.</exception>
        public static SiebelListQuery ToSiebel(ActivityQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            string?[] clauses =
            [
                Exact("Id", query.Id, nameof(query.Id)),
                Exact("Activity SR Id", query.ServiceRequestId, nameof(query.ServiceRequestId)),
                Exact("Case Id", query.CaseId, nameof(query.CaseId)),
                Exact("Plan Name", query.PlanName, nameof(query.PlanName)),
                Exact("Status", query.Status, nameof(query.Status)),
                Exact("Type", query.Type, nameof(query.Type)),
            ];

            return ToList(clauses, "An activity search needs at least one criterion.", nameof(query), query.PageSize, query.StartRowNum, query.ViewMode, query.IncludeTotalCount);
        }

        /// <summary>Converts a published activity plan search to the wire query.</summary>
        /// <param name="query">The search.</param>
        /// <returns>The wire query.</returns>
        /// <exception cref="ArgumentException">No criterion, or a blank or unsafe one.</exception>
        public static SiebelListQuery ToSiebel(ActivityPlanQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            string?[] clauses =
            [
                Exact("Id", query.Id, nameof(query.Id)),
                Exact("Activity SR Id", query.ServiceRequestId, nameof(query.ServiceRequestId)),
                Exact("Template", query.Template, nameof(query.Template)),
                Exact("Template Id", query.TemplateId, nameof(query.TemplateId)),
                Exact("Status", query.Status, nameof(query.Status)),
            ];

            return ToList(clauses, "An activity plan search needs at least one criterion.", nameof(query), query.PageSize, query.StartRowNum, query.ViewMode, query.IncludeTotalCount);
        }

        /// <summary>Refuses a query <see cref="ToSiebel(ActivityQuery)"/> would refuse — the service's preflight.</summary>
        /// <param name="query">The search.</param>
        public static void Validate(ActivityQuery query) => _ = ToSiebel(query);

        /// <summary>Refuses a query <see cref="ToSiebel(ActivityPlanQuery)"/> would refuse — the service's preflight.</summary>
        /// <param name="query">The search.</param>
        public static void Validate(ActivityPlanQuery query) => _ = ToSiebel(query);

        /// <summary>Converts the options of a single-activity read to the wire query.</summary>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <returns>The wire query.</returns>
        /// <remarks>
        /// No <c>fields</c> list: the child view under a service request rejects one
        /// (<c>500</c>, MEASURED 2026-09-29) where the standalone path accepts it, so every
        /// field comes back on both and the unmodelled ones land in
        /// <see cref="Activity.AdditionalFields"/>.
        /// </remarks>
        public static SiebelItemQuery ToReadSiebel(ActivityReadOptions? options) =>
            new() { ChildLinks = "None", ViewMode = ResolveViewMode(options?.ViewMode) };

        /// <summary>Converts the options of a service request's child read (activities or plans) to the wire query.</summary>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <param name="startRowNum">The zero-based row the page starts at.</param>
        /// <returns>The wire query.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startRowNum"/> is negative.</exception>
        public static SiebelListQuery ToChildSiebel(ActivityReadOptions? options, int startRowNum = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(startRowNum);

            return new SiebelListQuery
            {
                UniformResponse = SiebelFlag.Yes,
                ChildLinks = "None",
                PageSize = ChildPageSize,
                StartRowNum = startRowNum,
                ViewMode = ResolveViewMode(options?.ViewMode),
            };
        }

        /// <summary>Converts a create to the row Siebel takes.</summary>
        /// <param name="input">The fields to write.</param>
        /// <returns>The wire row, with only the set fields populated, plus the placeholder key and the two defaults.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <see cref="ActivityInput.Type"/> or <see cref="ActivityInput.ActionBy"/> is blank,
        /// or nothing names what the activity is profiled to.
        /// </exception>
        public static SiebelActivity ToSiebel(ActivityInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            RequireText(input.Type, nameof(input.Type));
            RequireText(input.ActionBy, nameof(input.ActionBy));
            if (string.IsNullOrWhiteSpace(input.ServiceRequestId) && string.IsNullOrWhiteSpace(input.CaseId)
                && string.IsNullOrWhiteSpace(input.IncidentId) && string.IsNullOrWhiteSpace(input.MemoId))
            {
                // An activity profiled to nothing is a stand-alone one: legal in ICM, but
                // not something MySS has a reason to file, and easy to do by mistake.
                throw new ArgumentException(
                    "An activity needs a service request, case, incident or memo to be profiled to.", nameof(input));
            }

            return new SiebelActivity
            {
                Id = CreatePlaceholderId,
                ActivitySRId = input.ServiceRequestId,
                CaseId = input.CaseId,
                IncidentId = input.IncidentId,
                ICMMemoId = input.MemoId,
                ActivityTemplateId = input.ActivityPlanId,
                Type = input.Type,
                ICMSubType = input.IcmSubType,
                Category = input.Category ?? DefaultCategory,
                ActionBy = input.ActionBy,
                ActionCode = input.ActionCode,
                ResultCode = input.ResultCode,
                Description = input.Description,
                Display = input.Display ?? DefaultDisplay,
                Status = input.Status,
                Priority = input.Priority,
                Sequence = input.Sequence,
                Planned = SiebelDate.FromDateTime(input.Planned),
                Due = SiebelDate.FromDateTime(input.Due),
                DoneFlag = SiebelFlag.FromBoolean(input.IsDone),
                PrimaryOwnedBy = input.PrimaryOwnedBy,
                ServiceOffice = input.ServiceOffice,
                ICMCreatedByOffice = input.CreatedByOffice,
                Ministry = input.Ministry,
            };
        }

        /// <summary>Refuses a create <see cref="ToSiebel(ActivityInput)"/> would refuse — the service's preflight.</summary>
        /// <param name="input">The fields to write.</param>
        public static void Validate(ActivityInput input) => _ = ToSiebel(input);

        /// <summary>Converts a page of activity rows to the published page.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <param name="totalCount">The total match count, when ICM's header carried one.</param>
        /// <returns>The published page; empty when there was nothing to convert.</returns>
        public static ActivityPage ToModel(SiebelActivityListResponse? siebel, long? totalCount = null) =>
            new() { Items = ToModels(siebel), TotalCount = totalCount };

        /// <summary>Converts the rows of an activity list response to the published models.</summary>
        /// <param name="siebel">The list response, or null.</param>
        /// <returns>The rows; empty when there was nothing to convert.</returns>
        public static IReadOnlyList<Activity> ToModels(SiebelActivityListResponse? siebel) =>
            siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)];

        /// <summary>Converts a page of plan rows to the published page.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <param name="totalCount">The total match count, when ICM's header carried one.</param>
        /// <returns>The published page; empty when there was nothing to convert.</returns>
        public static ActivityPlanPage ToModel(SiebelActivityPlanListResponse? siebel, long? totalCount = null) =>
            new() { Items = ToModels(siebel), TotalCount = totalCount };

        /// <summary>Converts the rows of a plan list response to the published models.</summary>
        /// <param name="siebel">The list response, or null.</param>
        /// <returns>The rows; empty when there was nothing to convert.</returns>
        public static IReadOnlyList<ActivityPlan> ToModels(SiebelActivityPlanListResponse? siebel) =>
            siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)];

        /// <summary>Converts a wire activity to the published model.</summary>
        /// <param name="siebel">The row ICM returned.</param>
        /// <returns>The published model.</returns>
        public static Activity ToModel(SiebelActivity siebel)
        {
            ArgumentNullException.ThrowIfNull(siebel);
            Dictionary<string, string> unparsed = [];

            return new Activity
            {
                Id = siebel.Id,
                ServiceRequestId = siebel.ActivitySRId,
                ServiceRequestNumber = siebel.SRNumber,
                ServiceRequestType = siebel.SRType,
                ServiceRequestSubType = siebel.SRSubType,
                CaseId = siebel.CaseId,
                CaseNumber = siebel.CaseNumber,
                IncidentId = siebel.IncidentId,
                MemoId = siebel.ICMMemoId,
                ActivityPlanId = siebel.ActivityTemplateId,
                PlanName = siebel.PlanName,
                Sequence = siebel.Sequence,
                Type = siebel.Type,
                IcmType = siebel.ICMType,
                IcmSubType = siebel.ICMSubType,
                Category = siebel.Category,
                Class = siebel.Class,
                ActionBy = siebel.ActionBy,
                ActionCode = siebel.ActionCode,
                ResultCode = siebel.ResultCode,
                Description = siebel.Description,
                Display = siebel.Display,
                Status = siebel.Status,
                IsDone = SiebelFlag.ToBoolean(siebel.DoneFlag, "Done Flag", unparsed),
                Done = SiebelDate.ToDateTime(siebel.Done, "Done", unparsed),
                Due = SiebelDate.ToDateTime(siebel.Due, "Due", unparsed),
                Planned = SiebelDate.ToDateTime(siebel.Planned, "Planned", unparsed),
                PlannedCompletion = SiebelDate.ToDateTime(siebel.PlannedCompletion, "Planned Completion", unparsed),
                DurationMinutes = siebel.DurationMinutes,
                Priority = siebel.Priority,
                OwnedBy = siebel.OwnedBy,
                OwnedById = siebel.OwnedById,
                PrimaryOwnedBy = siebel.PrimaryOwnedBy,
                PrimaryOwnerId = siebel.PrimaryOwnerId,
                PrimaryContactId = siebel.PrimaryContactId,
                ContactFirstName = siebel.ContactFirstName,
                ContactLastName = siebel.ContactLastName,
                ServiceOffice = siebel.ServiceOffice,
                ServiceRegion = siebel.ICMServiceRegion,
                Ministry = siebel.Ministry,
                MinistryId = siebel.MinistryId,
                CreatedByOffice = siebel.ICMCreatedByOffice,
                IsRestricted = SiebelFlag.ToBoolean(siebel.RestrictedFlag, "Restricted Flag", unparsed),
                IsShared = SiebelFlag.ToBoolean(siebel.SharedFlag, "Shared Flag", unparsed),
                IsRepeating = SiebelFlag.ToBoolean(siebel.Repeating, "Repeating", unparsed),
                HasAlarm = SiebelFlag.ToBoolean(siebel.Alarm, "Alarm", unparsed),
                IsOverdue = SiebelFlag.ToBoolean(siebel.Overdue, "Overdue", unparsed),
                ShowInContactProfile = SiebelFlag.ToBoolean(siebel.ShowInContactProfile, "Show In Contact Profile", unparsed),
                Created = SiebelDate.ToDateTime(siebel.Created, "Created", unparsed),
                CreatedById = siebel.CreatedBy,
                CreatedBy = siebel.CreatedByName,
                Updated = SiebelDate.ToDateTime(siebel.Updated, "Updated", unparsed),
                UpdatedById = siebel.UpdatedBy,
                UpdatedBy = siebel.UpdatedByName,
                AdditionalFields = Copy(siebel.AdditionalFields),
                UnparsedValues = unparsed,
            };
        }

        /// <summary>Converts a wire plan to the published model.</summary>
        /// <param name="siebel">The row ICM returned.</param>
        /// <returns>The published model.</returns>
        public static ActivityPlan ToModel(SiebelActivityPlan siebel)
        {
            ArgumentNullException.ThrowIfNull(siebel);
            Dictionary<string, string> unparsed = [];

            return new ActivityPlan
            {
                Id = siebel.Id,
                ServiceRequestId = siebel.ActivitySRId,
                ServiceRequestNumber = siebel.SRNumber,
                ServiceRequestType = siebel.SRType,
                ServiceRequestSubType = siebel.SRSubType,
                CaseId = siebel.CaseId,
                IncidentId = siebel.IncidentId,
                Template = siebel.Template,
                TemplateId = siebel.TemplateId,
                Status = siebel.Status,
                Comments = siebel.Comments,
                CompletedDate = SiebelDate.ToDateTime(siebel.CompletedDate, "Completed Date", unparsed),
                IsRestricted = SiebelFlag.ToBoolean(siebel.RestrictedFlag, "Restricted Flag", unparsed),
                CreatedByOffice = siebel.ICMCreatedByOffice,
                CreatedBy = siebel.CreatedByLogin,
                AdditionalFields = Copy(siebel.AdditionalFields),
                UnparsedValues = unparsed,
            };
        }

        private static SiebelListQuery ToList(
            string?[] clauses, string noCriterionMessage, string paramName, int? pageSize, int? startRowNum, string? viewMode, bool? includeTotalCount)
        {
            string searchSpec = string.Join(" AND ", clauses.Where(clause => clause is not null));
            if (searchSpec.Length == 0)
            {
                throw new ArgumentException(noCriterionMessage, paramName);
            }

            return new SiebelListQuery
            {
                UniformResponse = SiebelFlag.Yes,
                SearchSpec = searchSpec,
                ChildLinks = "None",
                PageSize = pageSize,
                StartRowNum = startRowNum,
                ViewMode = ResolveViewMode(viewMode),
                RecordCountNeeded = includeTotalCount,
            };
        }

        private static string ResolveViewMode(string? viewMode) =>
            string.IsNullOrWhiteSpace(viewMode) ? DefaultViewMode : viewMode;

        private static Dictionary<string, JsonElement> Copy(IDictionary<string, JsonElement>? fields) =>
            fields is null ? [] : new Dictionary<string, JsonElement>(fields);

        private static string? Exact(string field, string? value, string property) =>
            value is null ? null : $"[{field}] = \"{SiebelSearchValue.Vet(value, property)}\"";

        private static void RequireText(string? value, string property)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{property} is required to create an activity.", property);
            }
        }
    }
}
