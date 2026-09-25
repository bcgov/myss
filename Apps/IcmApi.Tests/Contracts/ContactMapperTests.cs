namespace Icm.Api.Tests.Contracts
{
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Icm.Api.Contracts;
    using Icm.Api.Models;

    /// <summary>
    /// The contact mapping: the search expression the library builds — the only thing
    /// standing between a caller's values and a Siebel expression, so what it refuses is
    /// pinned here — and the typing of what comes back.
    /// </summary>
    public class ContactMapperTests
    {
        // Synthetic, in the measured shape: 44 characters of base64, using all three of
        // the characters (+ / =) that make it awkward in a URL.
        private const string Did = "MYSS+synthetic/DID0123456789abcdefghijklmno=";

        [Fact]
        public void ToSiebel_MatchesABcscDidExactly()
        {
            SiebelListQuery query = ContactMapper.ToSiebel(new ContactQuery { BcServicesCardDid = Did });

            Assert.Equal($"[ICM BCSC DID] = \"{Did}\"", query.SearchSpec);
            Assert.Equal(SiebelFlag.Yes, query.UniformResponse);
        }

        [Fact]
        public void ToSiebel_JoinsEveryCriterionWithAnd()
        {
            SiebelListQuery query = ContactMapper.ToSiebel(new ContactQuery
            {
                PersonId = "0000000000001",
                Sin = "046454286",
                Phn = "9999999998",
                LastName = "IntegrationTest",
                BirthDate = new DateOnly(1950, 1, 31),
            });

            Assert.Equal(
                "[Person ID ICM] = \"0000000000001\""
                + " AND [SIN] = \"046454286\""
                + " AND [PHN] = \"9999999998\""
                + " AND [Last Name] ~= \"IntegrationTest\""
                + " AND [Birth Date] = \"01/31/1950\"",
                query.SearchSpec);
        }

        [Fact]
        public void ToSiebel_UsesTheDocumentsFieldNamesInTheExpression()
        {
            // searchspec and fields speak different vocabularies. MEASURED 2026-09-17:
            // [Primary Email] is SBL-DAT-00416 here, fields=Email Address is SBL-EAI-50258.
            SiebelListQuery query = ContactMapper.ToSiebel(new ContactQuery
            {
                Id = "1-TEST01",
                IntegrationId = "I-1",
                Email = "myss.test@example.invalid",
                CellPhone = "2505550199",
                HomePhone = "2505550198",
                WorkPhone = "2505550197",
                MessagePhone = "2505550196",
            });

            Assert.Equal(
                "[Id] = \"1-TEST01\""
                + " AND [Integration Id] = \"I-1\""
                + " AND [Email Address] ~= \"myss.test@example.invalid\""
                + " AND [Cellular Phone #] = \"2505550199\""
                + " AND [Home Phone #] = \"2505550198\""
                + " AND [Work Phone #] = \"2505550197\""
                + " AND [Message Phone] = \"2505550196\"",
                query.SearchSpec);
            Assert.Contains("Primary Email", query.Fields, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(ContactNameMatch.Exact, "[First Name] ~= \"Myss\" AND [Middle Name] ~= \"Q\" AND [Last Name] ~= \"Int\"")]
        public void ToSiebel_ComparesWholeNamesIgnoringCase(ContactNameMatch match, string expected)
        {
            // MEASURED: = misses "smith" against "Smith"; ~= matches it. A single letter
            // is fine for an exact match — it is somebody's whole middle name.
            SiebelListQuery query = ContactMapper.ToSiebel(new ContactQuery
            {
                FirstName = "Myss", MiddleName = "Q", LastName = "Int", NameMatch = match,
            });

            Assert.Equal(expected, query.SearchSpec);
        }

        [Theory]
        [InlineData(ContactNameMatch.StartsWith, "[First Name] ~LIKE \"My*\" AND [Last Name] ~LIKE \"Int*\"")]
        [InlineData(ContactNameMatch.Contains, "[First Name] ~LIKE \"*My*\" AND [Last Name] ~LIKE \"*Int*\"")]
        public void ToSiebel_AddsTheWildcardsItself_ForAPartialNameMatch(ContactNameMatch match, string expected)
        {
            SiebelListQuery query = ContactMapper.ToSiebel(new ContactQuery
            {
                FirstName = "My", LastName = "Int", NameMatch = match,
            });

            Assert.Equal(expected, query.SearchSpec);
        }

        [Fact]
        public void NameMatch_AppliesToNamesOnly()
        {
            SiebelListQuery query = ContactMapper.ToSiebel(new ContactQuery
            {
                Sin = "046454286", LastName = "Int", NameMatch = ContactNameMatch.StartsWith,
            });

            Assert.Equal("[SIN] = \"046454286\" AND [Last Name] ~LIKE \"Int*\"", query.SearchSpec);
        }

        [Theory]
        [InlineData(ContactNameMatch.StartsWith)]
        [InlineData(ContactNameMatch.Contains)]
        public void APartialNameMatch_NeedsTwoCharacters(ContactNameMatch match)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => ContactMapper.ToSiebel(new ContactQuery { LastName = "S", NameMatch = match }));

            Assert.Equal("LastName", exception.ParamName);
        }

        [Fact]
        public void ASearchWithNoCriterionIsRefused()
        {
            // Paging and visibility are not criteria: this would be every contact in ICM.
            Assert.Throws<ArgumentException>(() => ContactMapper.ToSiebel(
                new ContactQuery { PageSize = 100, ViewMode = "Organization", IncludeTotalCount = true }));
            Assert.Throws<ArgumentNullException>(() => ContactMapper.ToSiebel(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ABlankCriterionIsRefused_NotSentAndNotSkipped(string blank)
        {
            // Sent, [SIN] = "" matches everyone with no SIN on file. Skipped, a search by
            // SIN and name silently becomes a search by name. Both are wrong answers to a
            // value that went missing upstream.
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => ContactMapper.ToSiebel(new ContactQuery { Sin = blank, LastName = "IntegrationTest" }));

            Assert.Equal("Sin", exception.ParamName);
        }

        [Theory]
        [InlineData("x\" OR [First Name] LIKE \"*")] // MEASURED: this returned other people's records
        [InlineData("abc\"")]
        [InlineData("abc*")]
        [InlineData("a?c")]
        [InlineData("abc\ndef")]
        [InlineData("abc\0")]
        public void AValueThatCouldRewriteTheSearchIsRefused_OnEveryTextCriterion(string hostile)
        {
            // Every string property, found by reflection, so a criterion added later
            // cannot skip the check by not being listed here.
            PropertyInfo[] criteria = [.. typeof(ContactQuery).GetProperties()
                .Where(property => property.PropertyType == typeof(string)
                    && property.Name != nameof(ContactQuery.ViewMode))];
            Assert.Equal(14, criteria.Length);

            foreach (PropertyInfo criterion in criteria)
            {
                ContactQuery query = new();
                criterion.SetValue(query, hostile);

                ArgumentException exception = Assert.Throws<ArgumentException>(() => ContactMapper.ToSiebel(query));
                Assert.Equal(criterion.Name, exception.ParamName);
            }
        }

        [Fact]
        public void TheRefusalDoesNotRepeatTheValue()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => ContactMapper.ToSiebel(new ContactQuery { Sin = "046454286\"" }));

            Assert.DoesNotContain("046454286", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ToSiebel_AsksOnlyForTheFieldsTheModelCarries()
        {
            SiebelListQuery query = ContactMapper.ToSiebel(new ContactQuery { Sin = "046454286" });

            // Never null: a null field list means "all 80", which is ethnicity, religion
            // and medications travelling to a portal that has no use for them.
            Assert.Equal(string.Join(',', ContactMapper.RequestedFields), query.Fields);
            Assert.Equal(20, ContactMapper.RequestedFields.Count);
            Assert.Contains("SIN", ContactMapper.RequestedFields);
            Assert.Contains("PHN", ContactMapper.RequestedFields);
            Assert.DoesNotContain("Email Address", ContactMapper.RequestedFields);
            Assert.DoesNotContain("Ethnicity", ContactMapper.RequestedFields);
            Assert.DoesNotContain("Religious Affiliation", ContactMapper.RequestedFields);
            Assert.DoesNotContain("Medications", ContactMapper.RequestedFields);
            Assert.Equal("None", query.ChildLinks);
        }

        [Fact]
        public void TheRequestedFieldsAreExactlyTheOnesTheWireContractDeclares()
        {
            // A property without its name in the list is null for ever; a name without a
            // property is fetched and then shows up as an "unexpected" additional field.
            string[] declared = [.. typeof(SiebelContact)
                .GetProperties()
                .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
                .Where(name => name is not null and not "Link")
                .Select(name => name!)
                .Order(StringComparer.Ordinal)];

            Assert.Equal(declared, ContactMapper.RequestedFields.Order(StringComparer.Ordinal));
        }

        [Fact]
        public void ToSiebel_PassesPagingAndVisibilityThrough_AndLeavesThemOffWhenUnset()
        {
            SiebelListQuery bare = ContactMapper.ToSiebel(new ContactQuery { Id = "1-TEST01", ViewMode = " " });
            Assert.Null(bare.PageSize);
            Assert.Null(bare.StartRowNum);
            Assert.Null(bare.ViewMode);
            Assert.Null(bare.RecordCountNeeded);
            Assert.Null(bare.SortSpec);

            SiebelListQuery paged = ContactMapper.ToSiebel(new ContactQuery
            {
                Id = "1-TEST01", PageSize = 25, StartRowNum = 50, ViewMode = "Organization", IncludeTotalCount = true,
            });
            Assert.Equal(25, paged.PageSize);
            Assert.Equal(50, paged.StartRowNum);
            Assert.Equal("Organization", paged.ViewMode);
            Assert.True(paged.RecordCountNeeded);
        }

        [Fact]
        public void ToModel_TypesWhatSiebelSendsAsText()
        {
            SiebelContact siebel = new()
            {
                Id = "1-TEST01",
                PersonIdIcm = "0000000000001",
                IcmBcscDid = Did,
                Sin = "046454286",
                Phn = "9999999998",
                FirstName = "Myss",
                LastName = "IntegrationTest",
                BirthDate = "01/31/1950",
                MF = "Unknown",
                PrimaryEmail = "myss.test@example.invalid",
                DeceasedFlag = "N",
                PotentialDuplicateFlag = "Y",
                Created = "06/17/2026 14:30:00",
            };

            Contact model = ContactMapper.ToModel(siebel);

            Assert.Equal("1-TEST01", model.Id);
            Assert.Equal("0000000000001", model.PersonId);
            Assert.Equal(Did, model.BcServicesCardDid);
            Assert.Equal("046454286", model.Sin);
            Assert.Equal("9999999998", model.Phn);
            Assert.Equal("Unknown", model.Gender);
            Assert.Equal("myss.test@example.invalid", model.Email);

            // Month-first, and a date with no zone to shift it across midnight.
            Assert.Equal(new DateOnly(1950, 1, 31), model.BirthDate);
            Assert.False(model.IsDeceased);
            Assert.True(model.IsPotentialDuplicate);

            // DTYPE_UTCDATETIME with no offset on the wire: read as UTC.
            Assert.Equal(new DateTimeOffset(2026, 6, 17, 14, 30, 0, TimeSpan.Zero), model.Created);
            Assert.Null(model.Updated);
            Assert.Empty(model.UnparsedValues);
            Assert.Empty(model.AdditionalFields);
        }

        [Fact]
        public void ToModel_KeepsWhatItCannotReadRatherThanGuessing()
        {
            SiebelContact siebel = new() { BirthDate = "31st of January", DeceasedFlag = "Maybe" };

            Contact model = ContactMapper.ToModel(siebel);

            // Null, not false: "not deceased" is a claim, and "Maybe" does not support it.
            Assert.Null(model.BirthDate);
            Assert.Null(model.IsDeceased);
            Assert.Equal("31st of January", model.UnparsedValues["Birth Date"]);
            Assert.Equal("Maybe", model.UnparsedValues["Deceased Flag"]);
        }

        [Fact]
        public void AnUndeclaredFieldSurfacesInAdditionalFields_AndLinksDoNot()
        {
            SiebelContact siebel = JsonSerializer.Deserialize<SiebelContact>(
                """
                {
                  "Id": "1-TEST01",
                  "Some New Field": "value",
                  "Link": [ { "rel": "self", "href": "https://icm/x", "name": "ICMContact" } ]
                }
                """,
                IcmRefitSettings.JsonOptions)!;

            Contact model = ContactMapper.ToModel(siebel);

            Assert.Equal("value", Assert.Single(model.AdditionalFields).Value.GetString());
        }
    }
}
