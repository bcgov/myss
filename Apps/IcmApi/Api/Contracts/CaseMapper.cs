namespace Icm.Api.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Icm.Api.Models;

    /// <summary>
    /// Converts between Siebel's case records and the published <see cref="Case"/> and
    /// <see cref="CaseContact"/>, and turns a <see cref="CaseQuery"/> into the Siebel
    /// search expression — the only place one is built for cases, so the only place a
    /// value has to be vetted.
    /// </summary>
    internal static class CaseMapper
    {
        /// <summary>
        /// The visibility mode a case read uses unless told otherwise. MEASURED on SIT2 on
        /// 2026-09-24: an open Employment and Assistance case is invisible under ICM's
        /// default (<c>Sales Rep</c>), under <c>Organization</c> — the mode service requests
        /// need — and under <c>Personal</c>; <c>Manager</c>, <c>Group</c>,
        /// <c>Sub-Organization</c> and <c>Catalog</c> all see it.
        /// </summary>
        public const string DefaultViewMode = "Manager";

        /// <summary>
        /// The fields a case read asks ICM for, by their <b>live</b> names — exactly what
        /// <see cref="SiebelCase"/> declares. Adding a property there without adding its
        /// name here yields a property that is null for ever; <c>CaseMapperTests</c> holds
        /// the two together.
        /// </summary>
        public static readonly IReadOnlyList<string> RequestedFields =
        [
            "Id",
            "Case Num",
            "Name",
            "Type",
            "Status",
            "Close Reason",
            "Closed Date",
            "Early Open Reason",
            "Reopened Date",
            "Renew Review Date",
            "Caseload",
            "Work Queue",
            "Office Name",
            "Region Name",
            "Organization",
            "Legacy File Number",
            "Restricted Flag",
            "MyFS Flag",
            "Integration State",
            "Assigned To",
            "Assigned To Id",
            "Created Date",
            "Created By",
            "Created By Id",
            "Updated Date",
            "Updated By",
            "Updated By Id",
            "Last Updated Date",
            "Key Player Id",
            "Key Player Contact Row Num",
            "Key Player Integration Id",
            "Subject Contact First Name",
            "Subject Contact Last Name",
            "Middle Name",
            "Key Player AKA First Name",
            "Key Player AKA Last Name",
            "Key Player Birth Date",
            "Key Player M/F",
            "Key Player Age",
            "Key Player Deceased Flag",
            "Key Player Deceased Date",
            "Key Player Created Date",
            "Key Player Updated Date",
            "Key Player Last Updated Date",
        ];

        /// <summary>
        /// The fields a case's contact read asks ICM for — exactly what
        /// <see cref="SiebelCaseContact"/> declares, so the component's child-welfare
        /// fields never leave ICM.
        /// </summary>
        public static readonly IReadOnlyList<string> RequestedContactFields =
        [
            "Id",
            "Relationship",
            "Primary",
            "Start Date",
            "First Name",
            "Last Name",
            "Given Names",
            "AKA First Name",
            "AKA Last Name",
            "Date of Birth",
            "Age",
            "Gender",
            "SIN",
            "PHN",
            "Person ID ICM",
            "Person ID MIS",
            "BCeID User Name",
            "Home Phone",
            "Street Address",
            "City",
            "Country",
            "Primary Address",
            "Citizen",
            "Indigenous",
            "Subject",
            "Subject Child",
            "Parent_Caregiver",
            "Deceased",
            "Potential Duplicate",
            "Integration State",
            "Created Date",
            "Updated Date",
        ];

        /// <summary>Converts a published case search to the wire query.</summary>
        /// <param name="query">The search.</param>
        /// <returns>The wire query.</returns>
        /// <exception cref="ArgumentException">
        /// No criterion is set, or one is blank or carries a character that could change
        /// the meaning of the expression. The message names the property, never the value.
        /// </exception>
        public static SiebelListQuery ToSiebel(CaseQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            // searchspec names. All MEASURED on SIT2 2026-09-24 against the reference case:
            // each of the first five finds it on its own.
            string?[] clauses =
            [
                Exact("Id", query.Id, nameof(query.Id)),
                Exact("Case Num", query.CaseNumber, nameof(query.CaseNumber)),
                Exact("Key Player Id", query.KeyPlayerContactId, nameof(query.KeyPlayerContactId)),
                Exact("Key Player Contact Row Num", query.KeyPlayerPersonId, nameof(query.KeyPlayerPersonId)),
                Exact("Key Player Integration Id", query.KeyPlayerIntegrationId, nameof(query.KeyPlayerIntegrationId)),
                Exact("Status", query.Status, nameof(query.Status)),
                Exact("Type", query.Type, nameof(query.Type)),
            ];

            string searchSpec = string.Join(" AND ", clauses.Where(clause => clause is not null));
            if (searchSpec.Length == 0)
            {
                // Paging and ViewMode are not criteria. Without one this is "every case in
                // ICM", a page at a time, which nothing in MySS has a reason to ask for.
                throw new ArgumentException("A case search needs at least one criterion.", nameof(query));
            }

            return new SiebelListQuery
            {
                UniformResponse = SiebelFlag.Yes,
                SearchSpec = searchSpec,
                Fields = string.Join(',', RequestedFields),

                // Drops the ten child-collection links ICM otherwise attaches to every
                // case (Contact, Notes, ActivityPlan, Attachment, SupportNetwork,
                // Activities, Position, Organization, Memo, ReportableCircumstances).
                ChildLinks = "None",
                PageSize = query.PageSize,
                StartRowNum = query.StartRowNum,
                ViewMode = ResolveViewMode(query.ViewMode),
                RecordCountNeeded = query.IncludeTotalCount,
            };
        }

        /// <summary>Converts the options of a single-case read to the wire query.</summary>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <returns>The wire query.</returns>
        public static SiebelItemQuery ToReadSiebel(CaseReadOptions? options) =>
            new()
            {
                Fields = string.Join(',', RequestedFields),
                ChildLinks = "None",
                ViewMode = ResolveViewMode(options?.ViewMode),
            };

        /// <summary>Converts the options of a case's contact read to the wire query.</summary>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <returns>The wire query.</returns>
        /// <remarks>
        /// A child collection read is a list GET on the child, so it takes the list query.
        /// Nothing to search on: the case in the path is the whole criterion, and the
        /// child accepts no <c>searchspec</c> anyway (MEASURED 2026-09-24:
        /// <c>[Relationship]</c> is <c>SBL-DAT-00416</c>).
        /// </remarks>
        public static SiebelListQuery ToContactsSiebel(CaseReadOptions? options) =>
            new()
            {
                UniformResponse = SiebelFlag.Yes,
                Fields = string.Join(',', RequestedContactFields),
                ChildLinks = "None",
                ViewMode = ResolveViewMode(options?.ViewMode),
            };

        /// <summary>Converts a page of wire records to the published page.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <param name="totalCount">The total match count, when ICM's header carried one.</param>
        /// <returns>The published page; empty when there was nothing to convert.</returns>
        public static CasePage ToModel(SiebelCaseListResponse? siebel, long? totalCount = null) =>
            new()
            {
                Items = siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)],
                TotalCount = totalCount,
            };

        /// <summary>Converts a wire record to the published model.</summary>
        /// <param name="siebel">The record ICM returned.</param>
        /// <returns>The published model.</returns>
        public static Case ToModel(SiebelCase siebel)
        {
            ArgumentNullException.ThrowIfNull(siebel);
            Dictionary<string, string> unparsed = [];

            return new Case
            {
                Id = siebel.Id,
                CaseNumber = siebel.CaseNum,
                Name = siebel.Name,
                Type = siebel.Type,
                Status = siebel.Status,
                CloseReason = siebel.CloseReason,
                ClosedDate = SiebelDate.ToDate(siebel.ClosedDate, "Closed Date", unparsed),
                EarlyOpenReason = siebel.EarlyOpenReason,
                ReopenedDate = SiebelDate.ToDate(siebel.ReopenedDate, "Reopened Date", unparsed),
                RenewReviewDate = SiebelDate.ToDate(siebel.RenewReviewDate, "Renew Review Date", unparsed),
                Caseload = siebel.Caseload,
                WorkQueue = siebel.WorkQueue,
                OfficeName = siebel.OfficeName,
                RegionName = siebel.RegionName,
                Organization = siebel.Organization,
                LegacyFileNumber = siebel.LegacyFileNumber,
                IsRestricted = SiebelFlag.ToBoolean(siebel.RestrictedFlag, "Restricted Flag", unparsed),
                MyFsFlag = siebel.MyFSFlag,
                IntegrationState = siebel.IntegrationState,
                AssignedTo = siebel.AssignedTo,
                AssignedToId = siebel.AssignedToId,
                CreatedDate = SiebelDate.ToDateTime(siebel.CreatedDate, "Created Date", unparsed),
                CreatedBy = siebel.CreatedBy,
                CreatedById = siebel.CreatedById,
                UpdatedDate = SiebelDate.ToDateTime(siebel.UpdatedDate, "Updated Date", unparsed),
                UpdatedBy = siebel.UpdatedBy,
                UpdatedById = siebel.UpdatedById,
                LastUpdatedDate = SiebelDate.ToDateTime(siebel.LastUpdatedDate, "Last Updated Date", unparsed),
                KeyPlayerId = siebel.KeyPlayerId,
                KeyPlayerPersonId = siebel.KeyPlayerContactRowNum,
                KeyPlayerIntegrationId = siebel.KeyPlayerIntegrationId,
                SubjectFirstName = siebel.SubjectContactFirstName,
                SubjectLastName = siebel.SubjectContactLastName,
                SubjectMiddleName = siebel.MiddleName,
                KeyPlayerAkaFirstName = siebel.KeyPlayerAKAFirstName,
                KeyPlayerAkaLastName = siebel.KeyPlayerAKALastName,
                KeyPlayerBirthDate = SiebelDate.ToDate(siebel.KeyPlayerBirthDate, "Key Player Birth Date", unparsed),
                KeyPlayerGender = siebel.KeyPlayerMF,
                KeyPlayerAge = siebel.KeyPlayerAge,
                IsKeyPlayerDeceased = SiebelFlag.ToBoolean(siebel.KeyPlayerDeceasedFlag, "Key Player Deceased Flag", unparsed),
                KeyPlayerDeceasedDate = SiebelDate.ToDate(siebel.KeyPlayerDeceasedDate, "Key Player Deceased Date", unparsed),
                KeyPlayerCreated = SiebelDate.ToUtcDateTime(siebel.KeyPlayerCreatedDate, "Key Player Created Date", unparsed),
                KeyPlayerUpdated = SiebelDate.ToUtcDateTime(siebel.KeyPlayerUpdatedDate, "Key Player Updated Date", unparsed),
                KeyPlayerLastUpdated = SiebelDate.ToUtcDateTime(siebel.KeyPlayerLastUpdatedDate, "Key Player Last Updated Date", unparsed),
                AdditionalFields = Copy(siebel.AdditionalFields),
                UnparsedValues = unparsed,
            };
        }

        /// <summary>Converts the rows of a case's contact read to the published models.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <returns>The people on the case; empty when there was nothing to convert.</returns>
        public static IReadOnlyList<CaseContact> ToModel(SiebelCaseContactListResponse? siebel) =>
            siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)];

        /// <summary>Converts a wire contact row to the published model.</summary>
        /// <param name="siebel">The row ICM returned.</param>
        /// <returns>The published model.</returns>
        public static CaseContact ToModel(SiebelCaseContact siebel)
        {
            ArgumentNullException.ThrowIfNull(siebel);
            Dictionary<string, string> unparsed = [];

            return new CaseContact
            {
                Id = siebel.Id,
                Relationship = siebel.Relationship,
                IsPrimary = SiebelFlag.ToBoolean(siebel.Primary, "Primary", unparsed),
                StartDate = SiebelDate.ToDateTime(siebel.StartDate, "Start Date", unparsed),
                FirstName = siebel.FirstName,
                LastName = siebel.LastName,
                GivenNames = siebel.GivenNames,
                AkaFirstName = siebel.AKAFirstName,
                AkaLastName = siebel.AKALastName,
                BirthDate = SiebelDate.ToDate(siebel.DateofBirth, "Date of Birth", unparsed),
                Age = siebel.Age,
                Gender = siebel.Gender,
                Sin = siebel.SIN,
                Phn = siebel.PHN,
                PersonId = siebel.PersonIDICM,
                MisPersonId = siebel.PersonIDMIS,
                BceidUserName = siebel.BCeIDUserName,
                HomePhone = siebel.HomePhone,
                StreetAddress = siebel.StreetAddress,
                City = siebel.City,
                Country = siebel.Country,
                PrimaryAddress = siebel.PrimaryAddress,
                Citizen = siebel.Citizen,
                Indigenous = siebel.Indigenous,
                IsSubject = SiebelFlag.ToBoolean(siebel.Subject, "Subject", unparsed),
                IsSubjectChild = SiebelFlag.ToBoolean(siebel.SubjectChild, "Subject Child", unparsed),
                IsParentOrCaregiver = SiebelFlag.ToBoolean(siebel.ParentCaregiver, "Parent_Caregiver", unparsed),
                IsDeceased = SiebelFlag.ToBoolean(siebel.Deceased, "Deceased", unparsed),
                IsPotentialDuplicate = SiebelFlag.ToBoolean(siebel.PotentialDuplicate, "Potential Duplicate", unparsed),
                IntegrationState = siebel.IntegrationState,
                CreatedDate = SiebelDate.ToDateTime(siebel.CreatedDate, "Created Date", unparsed),
                UpdatedDate = SiebelDate.ToDateTime(siebel.UpdatedDate, "Updated Date", unparsed),
                AdditionalFields = Copy(siebel.AdditionalFields),
                UnparsedValues = unparsed,
            };
        }

        // Null and blank both mean "the default", which for cases is not ICM's.
        private static string ResolveViewMode(string? viewMode) =>
            string.IsNullOrWhiteSpace(viewMode) ? DefaultViewMode : viewMode;

        private static Dictionary<string, JsonElement> Copy(IDictionary<string, JsonElement>? fields) =>
            fields is null ? [] : new Dictionary<string, JsonElement>(fields);

        // Case-sensitive, which is right for identifiers and for ICM's own vocabulary
        // (Status, Type).
        private static string? Exact(string field, string? value, string property) =>
            value is null ? null : $"[{field}] = \"{SiebelSearchValue.Vet(value, property)}\"";
    }
}
