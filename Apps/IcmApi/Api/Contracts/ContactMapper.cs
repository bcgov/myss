namespace Icm.Api.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Icm.Api.Models;

    /// <summary>
    /// Converts between Siebel's contact records and the published <see cref="Contact"/>,
    /// and turns a <see cref="ContactQuery"/> into the Siebel search expression — the only
    /// place one is built for contacts, so the only place a value has to be vetted.
    /// </summary>
    internal static class ContactMapper
    {
        /// <summary>
        /// The fields a search asks ICM for, by their <b>live</b> names — exactly what
        /// <see cref="SiebelContact"/> declares, so nothing else about the person leaves
        /// ICM. Adding a property there without adding its name here yields a property
        /// that is null for ever; <c>ContactMapperTests</c> holds the two together.
        /// </summary>
        public static readonly IReadOnlyList<string> RequestedFields =
        [
            "Id",
            "Person ID ICM",
            "Integration Id",
            "ICM BCSC DID",
            "SIN",
            "PHN",
            "First Name",
            "Middle Name",
            "Last Name",
            "Birth Date",
            "M/F",
            "Primary Email",
            "Cellular Phone #",
            "Home Phone #",
            "Work Phone #",
            "Message Phone",
            "Deceased Flag",
            "Potential Duplicate Flag",
            "Created",
            "Updated",
        ];

        /// <summary>Converts a published contact search to the wire query.</summary>
        /// <param name="query">The search.</param>
        /// <returns>The wire query.</returns>
        /// <exception cref="ArgumentException">
        /// No criterion is set, or one is blank or carries a character that could change
        /// the meaning of the expression. The message names the property, never the value.
        /// </exception>
        public static SiebelListQuery ToSiebel(ContactQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            // searchspec names, which are the document's vocabulary rather than the
            // response's: [Email Address] here, "Primary Email" in RequestedFields. All
            // MEASURED searchable on SIT1 2026-09-17, as is every other field of the
            // business component.
            string?[] clauses =
            [
                Exact("Id", query.Id, nameof(query.Id)),
                Exact("Person ID ICM", query.PersonId, nameof(query.PersonId)),
                Exact("Integration Id", query.IntegrationId, nameof(query.IntegrationId)),
                Exact("ICM BCSC DID", query.BcServicesCardDid, nameof(query.BcServicesCardDid)),
                Exact("SIN", query.Sin, nameof(query.Sin)),
                Exact("PHN", query.Phn, nameof(query.Phn)),
                Name("First Name", query.FirstName, query.NameMatch, nameof(query.FirstName)),
                Name("Middle Name", query.MiddleName, query.NameMatch, nameof(query.MiddleName)),
                Name("Last Name", query.LastName, query.NameMatch, nameof(query.LastName)),

                // MM/DD/YYYY and nothing else: an ISO date here is a 500 (SBL-DAT-00359).
                query.BirthDate is { } birthDate
                    ? $"[Birth Date] = \"{SiebelDate.FromDate(birthDate)}\""
                    : null,
                IgnoringCase("Email Address", query.Email, nameof(query.Email)),
                Exact("Cellular Phone #", query.CellPhone, nameof(query.CellPhone)),
                Exact("Home Phone #", query.HomePhone, nameof(query.HomePhone)),
                Exact("Work Phone #", query.WorkPhone, nameof(query.WorkPhone)),
                Exact("Message Phone", query.MessagePhone, nameof(query.MessagePhone)),
            ];

            string searchSpec = string.Join(" AND ", clauses.Where(clause => clause is not null));
            if (searchSpec.Length == 0)
            {
                // Paging and ViewMode are not criteria. Without one this is "every contact
                // in ICM", a page at a time, which nothing in MySS has a reason to ask for.
                throw new ArgumentException(
                    "A contact search needs at least one criterion.", nameof(query));
            }

            return new SiebelListQuery
            {
                UniformResponse = SiebelFlag.Yes,
                SearchSpec = searchSpec,
                Fields = string.Join(',', RequestedFields),

                // MEASURED 2026-09-17: drops the five child-collection links ICM
                // otherwise attaches to every contact; self and canonical still come.
                ChildLinks = "None",
                PageSize = query.PageSize,
                StartRowNum = query.StartRowNum,
                ViewMode = string.IsNullOrWhiteSpace(query.ViewMode) ? null : query.ViewMode,
                RecordCountNeeded = query.IncludeTotalCount,
            };
        }

        /// <summary>Converts a page of wire records to the published page.</summary>
        /// <param name="siebel">The list response, or null when ICM sent no body.</param>
        /// <param name="totalCount">The total match count, when ICM's header carried one.</param>
        /// <returns>The published page; empty when there was nothing to convert.</returns>
        public static ContactPage ToModel(SiebelContactListResponse? siebel, long? totalCount = null) =>
            new()
            {
                Items = siebel?.Items is null ? [] : [.. siebel.Items.Select(ToModel)],
                TotalCount = totalCount,
            };

        /// <summary>Converts a wire record to the published model.</summary>
        /// <param name="siebel">The record ICM returned.</param>
        /// <returns>The published model.</returns>
        public static Contact ToModel(SiebelContact siebel)
        {
            Dictionary<string, string> unparsed = [];

            return new Contact
            {
                Id = siebel.Id,
                PersonId = siebel.PersonIdIcm,
                IntegrationId = siebel.IntegrationId,
                BcServicesCardDid = siebel.IcmBcscDid,
                Sin = siebel.Sin,
                Phn = siebel.Phn,
                FirstName = siebel.FirstName,
                MiddleName = siebel.MiddleName,
                LastName = siebel.LastName,
                BirthDate = SiebelDate.ToDate(siebel.BirthDate, "Birth Date", unparsed),
                Gender = siebel.MF,
                Email = siebel.PrimaryEmail,
                CellPhone = siebel.CellularPhone,
                HomePhone = siebel.HomePhone,
                WorkPhone = siebel.WorkPhone,
                MessagePhone = siebel.MessagePhone,
                IsDeceased = SiebelFlag.ToBoolean(siebel.DeceasedFlag, "Deceased Flag", unparsed),
                IsPotentialDuplicate = SiebelFlag.ToBoolean(
                    siebel.PotentialDuplicateFlag, "Potential Duplicate Flag", unparsed),
                Created = SiebelDate.ToUtcDateTime(siebel.Created, "Created", unparsed),
                Updated = SiebelDate.ToUtcDateTime(siebel.Updated, "Updated", unparsed),
                AdditionalFields = siebel.AdditionalFields is null
                    ? new Dictionary<string, JsonElement>()
                    : new Dictionary<string, JsonElement>(siebel.AdditionalFields),
                UnparsedValues = unparsed,
            };
        }

        // Case-sensitive, which is right for identifiers: MEASURED, = does not match
        // across case, and does not treat * as a pattern either.
        private static string? Exact(string field, string? value, string property) =>
            value is null ? null : $"[{field}] = \"{Vet(value, property)}\"";

        // ~= is Siebel's case-insensitive equality. MEASURED: it matches where = does not.
        private static string? IgnoringCase(string field, string? value, string property) =>
            value is null ? null : $"[{field}] ~= \"{Vet(value, property)}\"";

        private static string? Name(string field, string? value, ContactNameMatch match, string property)
        {
            if (value is null)
            {
                return null;
            }

            string vetted = Vet(value, property);
            if (match is ContactNameMatch.Exact)
            {
                return $"[{field}] ~= \"{vetted}\"";
            }

            // One letter of a name is most of the table. Two is still broad, but it is
            // where "I only know how it starts" becomes a search and not a listing.
            if (vetted.Trim().Length < 2)
            {
                throw new ArgumentException(
                    $"{property} needs at least two characters for a partial name match.", property);
            }

            // The wildcards are the library's, added after the value has been refused any
            // of its own. MEASURED: ~LIKE ignores case, LIKE does not.
            return match switch
            {
                ContactNameMatch.StartsWith => $"[{field}] ~LIKE \"{vetted}*\"",
                ContactNameMatch.Contains => $"[{field}] ~LIKE \"*{vetted}*\"",
                _ => throw new ArgumentOutOfRangeException(nameof(match), match, "Unknown name match."),
            };
        }

        // The security control, shared with the case mapper: see SiebelSearchValue.
        private static string Vet(string value, string property) => SiebelSearchValue.Vet(value, property);
    }
}
