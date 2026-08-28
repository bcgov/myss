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
                ICMCGAApplicationReceivedFlag = SiebelFlag.ToBoolean(siebel.ICMCGAApplicationReceivedFlag),
                ICMCGADueDiligenceDecision = siebel.ICMCGADueDiligenceDecision,
                ICMCGAResolutionDecisionDate = parsedICMCGAResolutionDecisionDate,
                ICMStage = siebel.ICMStage,
                Id = siebel.Id,
                IntegrationId = siebel.IntegrationId,
                Kkcfs = SiebelFlag.ToBoolean(siebel.Kkcfs),
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
                RestrictedFlag = SiebelFlag.ToBoolean(siebel.RestrictedFlag),
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
        /// <returns>The published page; empty when there was nothing to convert.</returns>
        public static ServiceRequestPage ToModel(SiebelListResponse? siebel) =>
            new()
            {
                Items = siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)],
                Links = siebel?.Link is null ? [] : [.. siebel.Link.Select(ToModel)],
            };

        /// <summary>Converts the fields a caller wants written to a wire record.</summary>
        /// <param name="input">The fields to write.</param>
        /// <returns>The wire record. Unset properties stay null and are not serialized.</returns>
        public static SiebelServiceRequest ToSiebel(ServiceRequestInput input) =>
            new()
            {
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
                SRSubSubType = input.SRSubSubType,
                SRSubType = input.SRSubType,
                ServiceOffice = input.ServiceOffice,
                ServiceRequestNumber = input.ServiceRequestNumber,
                Status = input.Status,
                Type = input.Type,
                TypeOfCaller = input.TypeOfCaller,
            };

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
        /// Field names are ICM's own, spaces and all, so they pass through untouched.
        /// </summary>
        private static string? ToFieldList(IEnumerable<string>? fields)
        {
            if (fields is null)
            {
                return null;
            }

            string joined = string.Join(',', fields.Where(f => !string.IsNullOrWhiteSpace(f)));
            return joined.Length == 0 ? null : joined;
        }
    }
}
