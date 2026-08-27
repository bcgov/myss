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
    /// <para>
    /// The one place the two shapes meet. Everything upstream of here deals in Siebel's
    /// terms — spaced field names, <c>"Y"</c>/<c>"N"</c> flags, dates as text, read-only
    /// fields mixed in with writable ones — and everything downstream deals in the
    /// published models, where those are booleans, dates and separate types.
    /// </para>
    /// <para>
    /// It is long and dull, and that is the trade this boundary makes: fifty lines of
    /// obvious mapping in one file, so that no caller anywhere else has to know that
    /// <c>Restricted Flag</c> is spelled with a space and answers <c>"Y"</c>.
    /// </para>
    /// </remarks>
    internal static class ServiceRequestMapper
    {
        /// <summary>Converts a wire record to the published read model.</summary>
        /// <param name="siebel">The record ICM returned.</param>
        /// <returns>The published model.</returns>
        public static ServiceRequest ToModel(SiebelServiceRequest siebel)
        {
            // Dates are read first, because an unreadable one has to be collected before
            // the model that carries the collection can be built.
            Dictionary<string, string> unparsed = [];
            DateTime? parsedCallDate =
                SiebelDate.ToDateTime(siebel.CallDate, "Call Date", unparsed);
            DateOnly? parsedResolutionDecisionDate =
                SiebelDate.ToDate(siebel.ICMCGAResolutionDecisionDate, "ICM CGA Resolution Decision Date", unparsed);
            DateTimeOffset? parsedCreated =
                SiebelDate.ToUtcDateTime(siebel.Created, "Created", unparsed);
            DateTimeOffset? parsedUpdated =
                SiebelDate.ToUtcDateTime(siebel.Updated, "Updated", unparsed);
            DateTimeOffset? parsedCloseDateCalc =
                SiebelDate.ToUtcDateTime(siebel.CloseDateCalc, "Close Date Calc", unparsed);

            return new ServiceRequest
            {
                Id = siebel.Id,
                AddressComments = siebel.AddressComments,
                ICMCPUAborginal = siebel.ICMCPUAborginal,
                CallDate = parsedCallDate,
                CPCallerAddress = siebel.CPCallerAddress,
                CPCallerEmail = siebel.CPCallerEmail,
                CPCallerName = siebel.CPCallerName,
                CPCallerPhone = siebel.CPCallerPhone,
                ContactCellNumber = siebel.ContactCellNumber,
                ICMCreatedByOffice = siebel.ICMCreatedByOffice,
                ContactGivenName = siebel.ContactGivenName,
                ContactHomePhone = siebel.ContactHomePhone,
                KKCFSFlag = SiebelFlag.ToBoolean(siebel.KKCFSFlag),
                CaseLocalOffice = siebel.CaseLocalOffice,
                Memo = siebel.Memo,
                CPNatureOfCall = siebel.CPNatureOfCall,
                CPPCCAnalysis = siebel.CPPCCAnalysis,
                CPCallerPrefContactMethod = siebel.CPCallerPrefContactMethod,
                RestrictedFlag = SiebelFlag.ToBoolean(siebel.RestrictedFlag),
                CPCallerType = siebel.CPCallerType,
                PrimaryContactId = siebel.PrimaryContactId,
                ICMStage = siebel.ICMStage,
                PrimaryOrganizationId = siebel.PrimaryOrganizationId,
                ICMCGADueDiligenceDecision = siebel.ICMCGADueDiligenceDecision,
                ICMCGAResolutionDecisionDate = parsedResolutionDecisionDate,
                PrimaryOrganizationName = siebel.PrimaryOrganizationName,
                ICMCGAApplicationReceivedFlag = SiebelFlag.ToBoolean(siebel.ICMCGAApplicationReceivedFlag),
                CPOutcome = siebel.CPOutcome,
                Created = parsedCreated,
                CreatedBy = siebel.CreatedBy,
                Updated = parsedUpdated,
                UpdatedByName = siebel.UpdatedByName,
                UpdatedBy = siebel.UpdatedBy,
                SRKPAddressCalc = siebel.SRKPAddressCalc,
                CloseDateCalc = parsedCloseDateCalc,
                CommMethod = siebel.CommMethod,
                ContactLastName = siebel.ContactLastName,
                CreatedByName = siebel.CreatedByName,
                IntegrationId = siebel.IntegrationId,
                CPCallerMethod = siebel.CPCallerMethod,
                AssignedToId = siebel.AssignedToId,
                AssignedTo = siebel.AssignedTo,
                Priority = siebel.Priority,
                ResolutionCode = siebel.ResolutionCode,
                SRNumber = siebel.SRNumber,
                SRType = siebel.SRType,
                SRSubType = siebel.SRSubType,
                SRSubSubType = siebel.SRSubSubType,
                Status = siebel.Status,
                ServiceOffice = siebel.ServiceOffice,
                ICMBCSCDID = siebel.ICMBCSCDID,
                Links = siebel.Link is null ? [] : [.. siebel.Link.Select(ToModel)],
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
                ICMCPUAborginal = input.ICMCPUAborginal,
                CallDate = SiebelDate.FromDateTime(input.CallDate),
                CPCallerAddress = input.CPCallerAddress,
                CPCallerEmail = input.CPCallerEmail,
                CPCallerName = input.CPCallerName,
                CPCallerPhone = input.CPCallerPhone,
                ContactCellNumber = input.ContactCellNumber,
                KKCFSFlag = SiebelFlag.FromBoolean(input.KKCFSFlag),
                Memo = input.Memo,
                CPNatureOfCall = input.CPNatureOfCall,
                CPPCCAnalysis = input.CPPCCAnalysis,
                CPCallerPrefContactMethod = input.CPCallerPrefContactMethod,
                RestrictedFlag = SiebelFlag.FromBoolean(input.RestrictedFlag),
                CPCallerType = input.CPCallerType,
                PrimaryContactId = input.PrimaryContactId,
                ICMStage = input.ICMStage,
                PrimaryOrganizationId = input.PrimaryOrganizationId,
                ICMCGADueDiligenceDecision = input.ICMCGADueDiligenceDecision,
                ICMCGAResolutionDecisionDate = SiebelDate.FromDate(input.ICMCGAResolutionDecisionDate),
                ICMCGAApplicationReceivedFlag = SiebelFlag.FromBoolean(input.ICMCGAApplicationReceivedFlag),
                CPOutcome = input.CPOutcome,
                CommMethod = input.CommMethod,
                ContactLastName = input.ContactLastName,
                IntegrationId = input.IntegrationId,
                CPCallerMethod = input.CPCallerMethod,
                Priority = input.Priority,
                ResolutionCode = input.ResolutionCode,
                SRNumber = input.SRNumber,
                SRType = input.SRType,
                SRSubType = input.SRSubType,
                SRSubSubType = input.SRSubSubType,
                Status = input.Status,
                ServiceOffice = input.ServiceOffice,
                ICMBCSCDID = input.ICMBCSCDID,
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
        /// Field names are Siebel's own, spaces and all, so they are passed through
        /// untouched.
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
