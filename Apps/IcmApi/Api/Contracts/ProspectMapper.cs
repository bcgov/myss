namespace Icm.Api.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Icm.Api.Models;

    /// <summary>
    /// Converts between Siebel's prospect rows and the published <see cref="Prospect"/>,
    /// turns a <see cref="ProspectInput"/> into the row to create, and turns a
    /// <see cref="ProspectQuery"/> into the Siebel search expression — the only place one
    /// is built for prospects, so the only place a value has to be vetted.
    /// </summary>
    internal static class ProspectMapper
    {
        /// <summary>
        /// The visibility mode a prospect read uses unless told otherwise: the mode every
        /// prospect row so far has been read under (MEASURED 2026-09-14 and 2026-09-29,
        /// always as the child of a service request, which needs <c>Organization</c>).
        /// Not ICM's default, <c>Sales Rep</c>, which hides service requests outright.
        /// </summary>
        public const string DefaultViewMode = "Organization";

        /// <summary>
        /// The rows a service request's prospect read asks for per page: the most the
        /// document allows. The child read wants every prospect on the request, not a page
        /// of them, so the repository follows <c>lastpage</c> from here.
        /// </summary>
        public const int ChildPageSize = 100;

        /// <summary>Converts a published prospect search to the wire query.</summary>
        /// <param name="query">The search.</param>
        /// <returns>The wire query.</returns>
        /// <exception cref="ArgumentException">
        /// No criterion is set, or one is blank or carries a character that could change
        /// the meaning of the expression. The message names the property, never the value.
        /// </exception>
        public static SiebelListQuery ToSiebel(ProspectQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            // searchspec names are the document's, which for this component are the live
            // ones. Not yet MEASURED: no standalone search has been run against ICM.
            string?[] clauses =
            [
                Exact("Id", query.Id, nameof(query.Id)),
                Exact("Service Request Id", query.ServiceRequestId, nameof(query.ServiceRequestId)),
                Exact("Prospective Contact UId", query.ProspectiveContactUId, nameof(query.ProspectiveContactUId)),
                Exact("Source", query.Source, nameof(query.Source)),
            ];

            string searchSpec = string.Join(" AND ", clauses.Where(clause => clause is not null));
            if (searchSpec.Length == 0)
            {
                throw new ArgumentException("A prospect search needs at least one criterion.", nameof(query));
            }

            return new SiebelListQuery
            {
                UniformResponse = SiebelFlag.Yes,
                SearchSpec = searchSpec,
                ChildLinks = "None",
                PageSize = query.PageSize,
                StartRowNum = query.StartRowNum,
                ViewMode = ResolveViewMode(query.ViewMode),
                RecordCountNeeded = query.IncludeTotalCount,
            };
        }

        /// <summary>
        /// Refuses a query <see cref="ToSiebel(ProspectQuery)"/> would refuse, with the same
        /// exceptions, and nothing else — for <see cref="Services.ProspectService"/> to run
        /// before it acquires a token.
        /// </summary>
        /// <param name="query">The search.</param>
        /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
        /// <exception cref="ArgumentException">No criterion is set, or one is blank or unsafe.</exception>
        public static void Validate(ProspectQuery query) => _ = ToSiebel(query);

        /// <summary>Converts the options of a single-prospect read to the wire query.</summary>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <returns>The wire query.</returns>
        /// <remarks>
        /// No <c>fields</c> list, unlike the case: the child view and the standalone
        /// component do not expose the same set (MEASURED 2026-09-29 — the child sends 36
        /// of the document's 42), and a name one of them lacks is <c>SBL-EAI-50258</c>.
        /// Every field comes back, the 42 modelled ones typed and the rest in
        /// <see cref="Prospect.AdditionalFields"/>.
        /// </remarks>
        public static SiebelItemQuery ToReadSiebel(ProspectReadOptions? options) =>
            new()
            {
                ChildLinks = "None",
                ViewMode = ResolveViewMode(options?.ViewMode),
            };

        /// <summary>Converts the options of a service request's prospect read to the wire query.</summary>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <param name="startRowNum">
        /// The zero-based row the page starts at: 0 for the first page, then the number of
        /// rows read so far for each continuation.
        /// </param>
        /// <returns>The wire query.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startRowNum"/> is negative.</exception>
        public static SiebelListQuery ToChildSiebel(ProspectReadOptions? options, int startRowNum = 0)
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

        /// <summary>
        /// The <c>Id</c> every standalone create sends: the literal ICM's REST Data API
        /// guidance prescribes as the user key of a new prospect, which Siebel replaces with
        /// the row id it allocates (MEASURED 2026-09-29, icmdev and SIT2). Never a caller's:
        /// the create is an upsert, so a real row id would rewrite that prospect.
        /// </summary>
        public const string CreatePlaceholderId = "NULL";

        /// <summary>Converts a create to the row Siebel takes.</summary>
        /// <param name="input">The fields to write.</param>
        /// <param name="nested">
        /// True when the row is nested inside a service request write, where the enclosing
        /// record is the parent and no service request id or placeholder key is sent; false
        /// for the standalone create, which needs both.
        /// </param>
        /// <returns>The wire row, with only the set fields populated.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// A required field is blank — the names, birth date and gender ICM's field list
        /// (ADO 8249) marks required — or a standalone create has no
        /// <see cref="ProspectInput.ServiceRequestId"/>, or a nested one has one.
        /// </exception>
        public static SiebelProspect ToSiebel(ProspectInput input, bool nested = false)
        {
            ArgumentNullException.ThrowIfNull(input);

            // ICM's field list (Prospects_8249_Fields.xlsx) marks these four required; the
            // audit fields Siebel sets itself.
            RequireText(input.FirstName, nameof(input.FirstName));
            RequireText(input.LastName, nameof(input.LastName));
            RequireText(input.Gender, nameof(input.Gender));
            RequireDate(input.BirthDate, nameof(input.BirthDate));

            if (!nested)
            {
                // Without it the row is filed under nothing, and nothing will ever find it.
                RequireText(input.ServiceRequestId, nameof(input.ServiceRequestId));
            }
            else if (input.ServiceRequestId is not null)
            {
                throw new ArgumentException(
                    $"{nameof(input.ServiceRequestId)} must be null on a prospect nested in a service request write.",
                    nameof(input));
            }

            return new SiebelProspect
            {
                Id = nested ? null : CreatePlaceholderId,
                ServiceRequestId = input.ServiceRequestId,
                FirstName = input.FirstName,
                MiddleName = input.MiddleName,
                LastName = input.LastName,
                Alias = input.Alias,
                PreviousFirstName = input.PreviousFirstName,
                PreviousMiddleName = input.PreviousMiddleName,
                PreviousLastName = input.PreviousLastName,
                BirthDate = SiebelDate.FromDate(input.BirthDate),
                MF = input.Gender,
                MM = input.Salutation,
                MaritalStatusCode = input.MaritalStatusCode,
                SocialSecurityNumber = input.Sin,
                ICMBCSCDID = input.BcServicesCardDid,
                ProspectiveContactUId = input.ProspectiveContactUId,
                ApplicantFlag = SiebelFlag.FromBoolean(input.IsApplicant),
                PortalRole = input.PortalRole,
                Purpose = input.Purpose,
                AddressCareOf = input.AddressCareOf,
                UnitNumber = input.UnitNumber,
                StreetAddress = input.StreetAddress,
                StreetAddress2 = input.StreetAddress2,
                StreetAddress3 = input.StreetAddress3,
                City = input.City,
                State = input.Province,
                PostalCode = input.PostalCode,
                EmailAddress = input.EmailAddress,
                HomePhone = input.HomePhone,
                CellularPhone = input.CellularPhone,
                WorkPhone = input.WorkPhone,
                AlternatePhone = input.AlternatePhone,
                PreferredCommunicationMethod = input.PreferredCommunicationMethod,
                Comment = input.Comment,
                Source = input.Source,
                SourceName = input.SourceName,
                SourceType = input.SourceType,
            };
        }

        /// <summary>
        /// Converts a prospect to the row nested inside a service request write — the
        /// parent is the enclosing record, so no service request id or placeholder key is
        /// sent. (The nested shape is MEASURED to be refused by ICM's integration object as
        /// it stands; the standalone create is the one that works.)
        /// </summary>
        /// <param name="input">The fields to write.</param>
        /// <returns>The wire row, with only the set fields populated.</returns>
        public static SiebelProspect ToNestedSiebel(ProspectInput input) => ToSiebel(input, nested: true);

        /// <summary>
        /// Refuses a create <see cref="ToSiebel(ProspectInput, bool)"/> would refuse, with
        /// the same exceptions — for the service's preflight.
        /// </summary>
        /// <param name="input">The fields to write.</param>
        public static void Validate(ProspectInput input) => _ = ToSiebel(input);

        /// <summary>Converts a page of wire rows to the published page.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <param name="totalCount">The total match count, when ICM's header carried one.</param>
        /// <returns>The published page; empty when there was nothing to convert.</returns>
        public static ProspectPage ToModel(SiebelProspectListResponse? siebel, long? totalCount = null) =>
            new()
            {
                Items = ToModels(siebel),
                TotalCount = totalCount,
            };

        /// <summary>Converts the rows of a list response to the published models.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <returns>The rows; empty when there was nothing to convert.</returns>
        public static IReadOnlyList<Prospect> ToModels(SiebelProspectListResponse? siebel) =>
            siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)];

        /// <summary>Converts a wire row to the published model.</summary>
        /// <param name="siebel">The row ICM returned.</param>
        /// <returns>The published model.</returns>
        public static Prospect ToModel(SiebelProspect siebel)
        {
            ArgumentNullException.ThrowIfNull(siebel);
            Dictionary<string, string> unparsed = [];

            return new Prospect
            {
                Id = siebel.Id,
                ServiceRequestId = siebel.ServiceRequestId,
                FirstName = siebel.FirstName,
                MiddleName = siebel.MiddleName,
                LastName = siebel.LastName,
                Alias = siebel.Alias,
                PreviousFirstName = siebel.PreviousFirstName,
                PreviousMiddleName = siebel.PreviousMiddleName,
                PreviousLastName = siebel.PreviousLastName,
                BirthDate = SiebelDate.ToDate(siebel.BirthDate, "Birth Date", unparsed),
                Gender = siebel.MF,
                Salutation = siebel.MM,
                MaritalStatusCode = siebel.MaritalStatusCode,
                Sin = siebel.SocialSecurityNumber,
                BcServicesCardDid = siebel.ICMBCSCDID,
                ProspectiveContactUId = siebel.ProspectiveContactUId,
                IsApplicant = SiebelFlag.ToBoolean(siebel.ApplicantFlag, "Applicant Flag", unparsed),
                PortalRole = siebel.PortalRole,
                Purpose = siebel.Purpose,
                AddressCareOf = siebel.AddressCareOf,
                UnitNumber = siebel.UnitNumber,
                StreetAddress = siebel.StreetAddress,
                StreetAddress2 = siebel.StreetAddress2,
                StreetAddress3 = siebel.StreetAddress3,
                City = siebel.City,
                Province = siebel.State,
                PostalCode = siebel.PostalCode,
                EmailAddress = siebel.EmailAddress,
                HomePhone = siebel.HomePhone,
                CellularPhone = siebel.CellularPhone,
                WorkPhone = siebel.WorkPhone,
                AlternatePhone = siebel.AlternatePhone,
                PreferredCommunicationMethod = siebel.PreferredCommunicationMethod,
                Comment = siebel.Comment,
                Source = siebel.Source,
                SourceName = siebel.SourceName,
                SourceType = siebel.SourceType,

                // Zone-less on the evidence in Prospect.Created's remarks, whatever the
                // document's DTYPE_UTCDATETIME says.
                Created = SiebelDate.ToDateTime(siebel.Created, "Created", unparsed),
                CreatedById = siebel.CreatedBy,
                CreatedBy = siebel.CreatedByName,
                LastUpdated = SiebelDate.ToDateTime(siebel.LastUpdated, "Last Updated", unparsed),
                LastUpdatedById = siebel.LastUpdatedBy,
                LastUpdatedBy = siebel.LastUpdatedByName,
                AdditionalFields = siebel.AdditionalFields is null ? [] : new Dictionary<string, JsonElement>(siebel.AdditionalFields),
                UnparsedValues = unparsed,
            };
        }

        private static string ResolveViewMode(string? viewMode) =>
            string.IsNullOrWhiteSpace(viewMode) ? DefaultViewMode : viewMode;

        private static string? Exact(string field, string? value, string property) =>
            value is null ? null : $"[{field}] = \"{SiebelSearchValue.Vet(value, property)}\"";

        private static void RequireText(string? value, string property)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{property} is required to create a prospect.", property);
            }
        }

        private static void RequireDate(DateOnly? value, string property)
        {
            if (value is null)
            {
                throw new ArgumentException($"{property} is required to create a prospect.", property);
            }
        }
    }
}
