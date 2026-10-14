namespace Icm.Api.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Icm.Api.Models;

    /// <summary>
    /// Converts between Siebel's wire records and the models this assembly publishes.
    /// </summary>
    /// <remarks>
    /// The one place the two shapes meet. Everything upstream deals in Siebel's terms —
    /// spaced field names, <c>"Y"</c>/<c>"N"</c> flags, dates as text — and everything
    /// downstream deals in the published models. Long and dull on purpose: fifty lines of
    /// obvious mapping in one file, so no caller elsewhere has to know that the SR number
    /// is spelled <c>Service Request Number</c>.
    /// </remarks>
    internal static class ServiceRequestMapper
    {
        /// <summary>Converts a wire record to the published read model.</summary>
        /// <param name="siebel">The record ICM returned.</param>
        /// <returns>The published model.</returns>
        public static ServiceRequest ToModel(SiebelServiceRequest siebel)
        {
            // Dates are read first: an unreadable one has to be collected before the
            // model that carries the collection can be built.
            Dictionary<string, string> unparsed = [];
            DateTime? parsedCallDate =
                SiebelDate.ToDateTime(siebel.CallDate, "Call Date", unparsed);
            DateTime? parsedClosedDate =
                SiebelDate.ToDateTime(siebel.ClosedDate, "Closed Date", unparsed);
            DateTime? parsedCreatedDate =
                SiebelDate.ToDateTime(siebel.CreatedDate, "Created Date", unparsed);
            DateOnly? parsedICMCGAResolutionDecisionDate =
                SiebelDate.ToDate(siebel.ICMCGAResolutionDecisionDate, "ICM CGA Resolution Decision Date", unparsed);
            DateTime? parsedUpdatedDate =
                SiebelDate.ToDateTime(siebel.UpdatedDate, "Updated Date", unparsed);

            return new ServiceRequest
            {
                Address = siebel.Address,
                AddressComments = siebel.AddressComments,
                AreAnyOfTheFamilyMembersIndigenous = siebel.AreAnyOfTheFamilyMembersIndigenous,
                AssignedTo = siebel.AssignedTo,
                AssignedToId = siebel.AssignedToId,
                CallDate = parsedCallDate,
                CallerAddress = siebel.CallerAddress,
                CallerEmail = siebel.CallerEmail,
                CallerName = siebel.CallerName,
                CallerPhone = siebel.CallerPhone,
                CaseLocalOffice = siebel.CaseLocalOffice,
                CellPhone = siebel.CellPhone,
                ClosedDate = parsedClosedDate,
                CommMethod = siebel.CommMethod,
                CreatedBy = siebel.CreatedBy,
                CreatedById = siebel.CreatedById,
                CreatedByOffice = siebel.CreatedByOffice,
                CreatedDate = parsedCreatedDate,
                GivenNames = siebel.GivenNames,
                HomePhone = siebel.HomePhone,
                ICMBCSCDID = siebel.ICMBCSCDID,
                ICMCGAApplicationReceivedFlag = SiebelFlag.ToBoolean(
                    siebel.ICMCGAApplicationReceivedFlag,
                    "ICM CGA Application Received Flag",
                    unparsed),
                ICMCGADueDiligenceDecision = siebel.ICMCGADueDiligenceDecision,
                ICMCGAResolutionDecisionDate = parsedICMCGAResolutionDecisionDate,
                ICMStage = siebel.ICMStage,
                Id = siebel.Id,
                IntegrationId = siebel.IntegrationId,
                Kkcfs = SiebelFlag.ToBoolean(siebel.Kkcfs, "Kkcfs", unparsed),
                LastName = siebel.LastName,
                Memo = siebel.Memo,
                Method = siebel.Method,
                NatureOfCall = siebel.NatureOfCall,
                PccSummary = siebel.PccSummary,
                PreferredContactMethod = siebel.PreferredContactMethod,
                PrimaryContactId = siebel.PrimaryContactId,
                PrimaryOrganizationId = siebel.PrimaryOrganizationId,
                PrimaryOrganizationName = siebel.PrimaryOrganizationName,
                Priority = siebel.Priority,
                Resolution = siebel.Resolution,
                RestrictedFlag = SiebelFlag.ToBoolean(
                    siebel.RestrictedFlag, "Restricted Flag", unparsed),
                RowId = siebel.RowId,
                SRSubSubType = siebel.SRSubSubType,
                SRSubType = siebel.SRSubType,
                ServiceOffice = siebel.ServiceOffice,
                ServiceRequestNumber = siebel.ServiceRequestNumber,
                Status = siebel.Status,
                Type = siebel.Type,
                TypeOfCaller = siebel.TypeOfCaller,
                UpdatedBy = siebel.UpdatedBy,
                UpdatedById = siebel.UpdatedById,
                UpdatedDate = parsedUpdatedDate,
                Links = siebel.Link is null ? [] : [.. siebel.Link.Select(ToModel)],
                AdditionalFields = siebel.AdditionalFields is null
                    ? new Dictionary<string, System.Text.Json.JsonElement>()
                    : new Dictionary<string, System.Text.Json.JsonElement>(siebel.AdditionalFields),
                UnparsedValues = unparsed,
            };
        }

        /// <summary>Converts a wire link to the published one.</summary>
        /// <param name="siebel">The link ICM returned.</param>
        /// <returns>The published model.</returns>
        public static ServiceRequestLink ToModel(SiebelLink siebel) =>
            new() { Rel = siebel.Rel, Href = siebel.Href, Name = siebel.Name };

        /// <summary>Converts a page of wire records to the published page.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <param name="totalCount">
        /// The total match count, when the caller asked for one and ICM's
        /// <c>Total-Record-Count</c> header carried it — a header, so the repository reads
        /// it, not this mapper.
        /// </param>
        /// <returns>The published page; empty when there was nothing to convert.</returns>
        public static ServiceRequestPage ToModel(SiebelListResponse? siebel, long? totalCount = null) =>
            new()
            {
                Items = siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)],
                Links = siebel?.Link is null ? [] : [.. siebel.Link.Select(ToModel)],
                TotalCount = totalCount,
            };

        /// <summary>Converts the fields a caller wants written to a wire record.</summary>
        /// <param name="input">The fields to write.</param>
        /// <param name="forCreate">
        /// True for a POST, which then carries the placeholder <see cref="CreatePlaceholderId"/>
        /// unless the input names an id, and an (empty) <c>ListOfSRProspects</c> container;
        /// false for a PUT, which carries only what was set.
        /// </param>
        /// <returns>The wire record. Unset properties stay null and are not serialized.</returns>
        public static SiebelServiceRequestWrite ToSiebel(ServiceRequestInput input, bool forCreate = false)
        {
            ArgumentNullException.ThrowIfNull(input);

            SiebelProspectList? prospects = input.Prospects is { Count: > 0 } rows
                ? new SiebelProspectList { SRProspects = [.. rows.Select(ProspectMapper.ToNestedSiebel)] }
                : forCreate ? new SiebelProspectList() : null;

            return new SiebelServiceRequestWrite
            {
                // A create needs a user key and a child container whatever the caller set:
                // see SiebelServiceRequestWrite and CreatePlaceholderId.
                Id = input.Id ?? (forCreate ? CreatePlaceholderId : null),
                AreAnyOfTheFamilyMembersIndigenous = input.AreAnyOfTheFamilyMembersIndigenous,
                CallDate = SiebelDate.FromDateTime(input.CallDate),
                CallerAddress = input.CallerAddress,
                CallerEmail = input.CallerEmail,
                CallerName = input.CallerName,
                CallerPhone = input.CallerPhone,
                CellPhone = input.CellPhone,
                CommMethod = input.CommMethod,
                ICMBCSCDID = input.ICMBCSCDID,
                ICMCGAApplicationReceivedFlag = SiebelFlag.FromBoolean(input.ICMCGAApplicationReceivedFlag),
                ICMCGADueDiligenceDecision = input.ICMCGADueDiligenceDecision,
                ICMCGAResolutionDecisionDate = SiebelDate.FromDate(input.ICMCGAResolutionDecisionDate),
                ICMStage = input.ICMStage,
                IntegrationId = input.IntegrationId,
                Kkcfs = SiebelFlag.FromBoolean(input.Kkcfs),
                LastName = input.LastName,
                ListOfSRProspects = prospects,
                Memo = input.Memo,
                Method = input.Method,
                NatureOfCall = input.NatureOfCall,
                PccSummary = input.PccSummary,
                PreferredContactMethod = input.PreferredContactMethod,
                PrimaryContactId = input.PrimaryContactId,
                PrimaryOrganizationId = input.PrimaryOrganizationId,
                Priority = input.Priority,
                Resolution = input.Resolution,
                RestrictedFlag = SiebelFlag.FromBoolean(input.RestrictedFlag),
                Type = input.Type,
                SRSubType = input.SRSubType,
                SRSubSubType = input.SRSubSubType,
                ServiceOffice = input.ServiceOffice,
                ServiceRequestNumber = input.ServiceRequestNumber,
                Status = input.Status,
                TypeOfCaller = input.TypeOfCaller,
            };
        }

        /// <summary>
        /// The <c>Id</c> a create sends when the caller gave none: the integration
        /// component's user key, which Siebel replaces with the row id it allocates
        /// (MEASURED SIT2 2026-09-29 with <c>1-53MU3WL</c>). Any value in Siebel's row-id
        /// shape that names no existing record will do; an existing one is refused as a
        /// duplicate (<c>SBL-EAI-04383</c>).
        /// </summary>
        public const string CreatePlaceholderId = "1-MYSS";

        /// <summary>Converts published search parameters to the wire query.</summary>
        /// <param name="query">The search parameters, or null for a bare search.</param>
        /// <returns>The wire query.</returns>
        public static SiebelListQuery ToSiebel(ServiceRequestQuery? query) =>
            new()
            {
                // uniformresponse is fixed rather than exposed: the spec permits only "Y",
                // and it is what makes a single-record result arrive in the same array
                // shape as a multi-record one, which SiebelListResponse relies on.
                UniformResponse = SiebelFlag.Yes,
                SearchSpec = query?.SearchSpec,
                SortSpec = query?.SortSpec,
                Fields = ToFieldList(query?.Fields),
                ChildLinks = query?.ChildLinks,
                PageSize = query?.PageSize,
                StartRowNum = query?.StartRowNum,
                ViewMode = query?.ViewMode,
                RecordCountNeeded = query?.IncludeTotalCount,
                ExcludeEmptyFieldsInResponse = query?.ExcludeEmptyFields,
            };

        /// <summary>Converts published read options to the wire query.</summary>
        /// <param name="options">The read options, or null for the defaults.</param>
        /// <returns>The wire query.</returns>
        public static SiebelItemQuery ToSiebel(ServiceRequestReadOptions? options) =>
            new()
            {
                Fields = ToFieldList(options?.Fields),
                ChildLinks = options?.ChildLinks,
                ViewMode = options?.ViewMode,
                ExcludeEmptyFieldsInResponse = options?.ExcludeEmptyFields,
            };

        /// <summary>
        /// Joins requested field names into the comma-separated list Siebel expects.
        /// Field names are ICM's own — the spaces <i>inside</i> a name like
        /// <c>SR Number</c> are load-bearing — but stray whitespace around a name would
        /// travel to ICM as part of it, so entries are trimmed.
        /// </summary>
        /// <summary>The <c>SR Relationship</c> a contact link sends when the caller gave none: what the workflow files for the matched applicant.</summary>
        public const string DefaultContactRelationship = "Key player";

        /// <summary>Converts a contact link to the row the SR's <c>ICMContact</c> child takes.</summary>
        /// <param name="link">Who to link, and how.</param>
        /// <returns>The wire row: the contact's id and the link fields, in the child view's names.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="link"/> is null.</exception>
        /// <exception cref="ArgumentException"><see cref="ServiceRequestContactLink.ContactId"/> is blank.</exception>
        public static SiebelServiceRequestContact ToSiebel(ServiceRequestContactLink link)
        {
            ArgumentNullException.ThrowIfNull(link);
            if (string.IsNullOrWhiteSpace(link.ContactId))
            {
                throw new ArgumentException("A contact link needs the contact's row id.", nameof(link));
            }

            // The child view's names, not the guidance's "Relationship"/"Subject", which
            // ICM accepts and ignores (MEASURED 2026-09-29).
            return new SiebelServiceRequestContact
            {
                Id = link.ContactId,
                SRRelationship = link.Relationship ?? DefaultContactRelationship,
                SSAPrimaryField = SiebelFlag.FromBoolean(link.IsPrimary ?? true),
                SRSubjectFlag = SiebelFlag.FromBoolean(link.IsSubject),
            };
        }

        /// <summary>Converts the options of an SR's contact read to the wire query.</summary>
        /// <param name="options">The read options, or null for the defaults.</param>
        /// <param name="startRowNum">The zero-based row the page starts at.</param>
        /// <returns>The wire query: a full page, no search, the caller's visibility mode.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startRowNum"/> is negative.</exception>
        public static SiebelListQuery ToContactsSiebel(ServiceRequestReadOptions? options, int startRowNum = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(startRowNum);

            return new SiebelListQuery
            {
                UniformResponse = SiebelFlag.Yes,
                ChildLinks = "None",
                PageSize = 100,
                StartRowNum = startRowNum,
                ViewMode = options?.ViewMode,
                ExcludeEmptyFieldsInResponse = options?.ExcludeEmptyFields,
            };
        }

        /// <summary>Converts the rows of an SR's contact read to the published models.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <returns>The contacts on the SR; empty when there was nothing to convert.</returns>
        public static IReadOnlyList<ServiceRequestContact> ToModel(SiebelServiceRequestContactListResponse? siebel) =>
            siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)];

        /// <summary>Converts a wire contact-link row to the published model.</summary>
        /// <param name="siebel">The row ICM returned.</param>
        /// <returns>The published model.</returns>
        public static ServiceRequestContact ToModel(SiebelServiceRequestContact siebel)
        {
            ArgumentNullException.ThrowIfNull(siebel);
            Dictionary<string, string> unparsed = [];

            return new ServiceRequestContact
            {
                Id = siebel.Id,
                ServiceRequestId = siebel.SRId,
                Relationship = siebel.SRRelationship,
                IsPrimary = SiebelFlag.ToBoolean(siebel.SSAPrimaryField, "SSA Primary Field", unparsed),
                IsSubject = SiebelFlag.ToBoolean(siebel.SRSubjectFlag, "SR Subject Flag", unparsed),
                Start = SiebelDate.ToDateTime(siebel.SRStart, "SR Start", unparsed),
                End = SiebelDate.ToDateTime(siebel.SREnd, "SR End", unparsed),
                FirstName = siebel.FirstName,
                MiddleName = siebel.MiddleName,
                LastName = siebel.LastName,
                BirthDate = SiebelDate.ToDate(siebel.BirthDate, "Birth Date", unparsed),
                Age = siebel.Age,
                Gender = siebel.MF,
                BcServicesCardDid = siebel.ICMBCSCDID,
                Indigenous = siebel.AboriginalCalc,
                ContactCreated = SiebelDate.ToDateTime(siebel.Created, "Created", unparsed),
                ContactCreatedBy = siebel.ICMCreatedBy,
                ContactUpdated = SiebelDate.ToDateTime(siebel.Updated, "Updated", unparsed),
                ContactUpdatedBy = siebel.ICMUpdatedBy,
                AdditionalFields = siebel.AdditionalFields is null
                    ? new Dictionary<string, System.Text.Json.JsonElement>()
                    : new Dictionary<string, System.Text.Json.JsonElement>(siebel.AdditionalFields),
                UnparsedValues = unparsed,
            };
        }

        private static string? ToFieldList(IEnumerable<string>? fields)
        {
            if (fields is null)
            {
                return null;
            }

            string joined = string.Join(
                ',',
                fields.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()));
            return joined.Length == 0 ? null : joined;
        }
    }
}
