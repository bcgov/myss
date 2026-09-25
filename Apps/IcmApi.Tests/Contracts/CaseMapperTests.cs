namespace Icm.Api.Tests.Contracts
{
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Icm.Api.Contracts;
    using Icm.Api.Models;

    /// <summary>
    /// The case mapping: the search expression the library builds and what it refuses,
    /// the field lists it asks for, and the typing of what comes back.
    /// </summary>
    public class CaseMapperTests
    {
        [Fact]
        public void ToSiebel_MatchesEachCriterionExactly_AndJoinsThemWithAnd()
        {
            SiebelListQuery query = CaseMapper.ToSiebel(new CaseQuery
            {
                Id = "1-5371KIQ",
                CaseNumber = "1-11077140770",
                KeyPlayerContactId = "1-532MU4J",
                KeyPlayerPersonId = "1-11069734915",
                KeyPlayerIntegrationId = "01232317",
                Status = "Open",
                Type = "Employment and Assistance",
            });

            Assert.Equal(
                "[Id] = \"1-5371KIQ\""
                + " AND [Case Num] = \"1-11077140770\""
                + " AND [Key Player Id] = \"1-532MU4J\""
                + " AND [Key Player Contact Row Num] = \"1-11069734915\""
                + " AND [Key Player Integration Id] = \"01232317\""
                + " AND [Status] = \"Open\""
                + " AND [Type] = \"Employment and Assistance\"",
                query.SearchSpec);
            Assert.Equal(SiebelFlag.Yes, query.UniformResponse);
            Assert.Equal("None", query.ChildLinks);
        }

        [Fact]
        public void ASearchWithNoCriterionIsRefused()
        {
            Assert.Throws<ArgumentException>(() => CaseMapper.ToSiebel(
                new CaseQuery { PageSize = 100, ViewMode = "Group", IncludeTotalCount = true }));
            Assert.Throws<ArgumentNullException>(() => CaseMapper.ToSiebel(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("x\" OR [Status] LIKE \"*")]
        [InlineData("abc*")]
        [InlineData("a?c")]
        [InlineData("abc\ndef")]
        public void AValueThatIsBlankOrCouldRewriteTheSearchIsRefused_OnEveryTextCriterion(string hostile)
        {
            // Every string property, found by reflection, so a criterion added later
            // cannot skip the check by not being listed here.
            PropertyInfo[] criteria = [.. typeof(CaseQuery).GetProperties()
                .Where(property => property.PropertyType == typeof(string)
                    && property.Name != nameof(CaseQuery.ViewMode))];
            Assert.Equal(7, criteria.Length);

            foreach (PropertyInfo criterion in criteria)
            {
                CaseQuery query = new();
                criterion.SetValue(query, hostile);

                ArgumentException exception = Assert.Throws<ArgumentException>(() => CaseMapper.ToSiebel(query));
                Assert.Equal(criterion.Name, exception.ParamName);
                Assert.DoesNotContain("abc", exception.Message, StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void TheVisibilityModeDefaultsToManager_NotToIcms(string? viewMode)
        {
            // MEASURED on SIT2 2026-09-24: the reference case is a 404 under Sales Rep,
            // Organization and Personal; Manager sees it.
            Assert.Equal("Manager", CaseMapper.ToSiebel(new CaseQuery { Id = "1-5371KIQ", ViewMode = viewMode }).ViewMode);
            Assert.Equal("Manager", CaseMapper.ToReadSiebel(new CaseReadOptions { ViewMode = viewMode }).ViewMode);
            Assert.Equal("Manager", CaseMapper.ToContactsSiebel(new CaseReadOptions { ViewMode = viewMode }).ViewMode);
            Assert.Equal("Manager", CaseMapper.ToReadSiebel(null).ViewMode);
            Assert.Equal("Manager", CaseMapper.ToContactsSiebel(null).ViewMode);
        }

        [Fact]
        public void AnExplicitVisibilityModeAndPagingPassThrough()
        {
            SiebelListQuery paged = CaseMapper.ToSiebel(new CaseQuery
            {
                Id = "1-5371KIQ", PageSize = 25, StartRowNum = 50, ViewMode = "Catalog", IncludeTotalCount = true,
            });

            Assert.Equal(25, paged.PageSize);
            Assert.Equal(50, paged.StartRowNum);
            Assert.Equal("Catalog", paged.ViewMode);
            Assert.True(paged.RecordCountNeeded);
            Assert.Null(paged.SortSpec);
        }

        [Fact]
        public void TheRequestedFieldsAreExactlyTheOnesTheWireContractsDeclare()
        {
            // A property without its name in the list is null for ever; a name without a
            // property is fetched and then shows up as an "unexpected" additional field.
            Assert.Equal(Declared<SiebelCase>(), CaseMapper.RequestedFields.Order(StringComparer.Ordinal));
            Assert.Equal(Declared<SiebelCaseContact>(), CaseMapper.RequestedContactFields.Order(StringComparer.Ordinal));
        }

        [Fact]
        public void TheRequestedFieldsAvoidWhatIcmRejectsAndWhatMyssShouldNotRead()
        {
            // MEASURED 2026-09-24: these four document names are SBL-EAI-50258 in a field
            // list; the live names are Created Date / Updated Date and there is no Sales Rep.
            Assert.DoesNotContain("Created", CaseMapper.RequestedFields);
            Assert.DoesNotContain("Sales Rep", CaseMapper.RequestedFields);
            Assert.DoesNotContain("ICM Created By", CaseMapper.RequestedFields);
            Assert.DoesNotContain("ICM Updated By", CaseMapper.RequestedFields);
            Assert.Contains("Created Date", CaseMapper.RequestedFields);

            // The child-welfare fields on both components are not asked for.
            Assert.DoesNotContain("Key Player DIN", CaseMapper.RequestedFields);
            Assert.DoesNotContain("Key Player CSA Status", CaseMapper.RequestedFields);
            Assert.DoesNotContain("Person Responsible for Alleged Maltreatment", CaseMapper.RequestedContactFields);
            Assert.DoesNotContain("92_1 AGT", CaseMapper.RequestedContactFields);

            // Never null: a null field list means every field of the component.
            Assert.False(string.IsNullOrEmpty(CaseMapper.ToSiebel(new CaseQuery { Id = "1-X" }).Fields));
            Assert.False(string.IsNullOrEmpty(CaseMapper.ToReadSiebel(null).Fields));
            Assert.False(string.IsNullOrEmpty(CaseMapper.ToContactsSiebel(null).Fields));
        }

        [Fact]
        public void ToModel_TypesWhatSiebelSendsAsText()
        {
            // The live shape of the SIT2 reference case, 2026-09-24.
            SiebelCase siebel = new()
            {
                Id = "1-5371KIQ",
                CaseNum = "1-11077140770",
                Name = "PEIRCEE, WINNONA",
                Type = "Employment and Assistance",
                Status = "Open",
                ReopenedDate = "07/02/2026",
                RenewReviewDate = "07/02/2027",
                RestrictedFlag = "N",
                CreatedDate = "06/29/2026 13:48:10",
                LastUpdatedDate = "07/07/2026 10:37:05",
                KeyPlayerId = "1-532MU4J",
                KeyPlayerContactRowNum = "1-11069734915",
                KeyPlayerBirthDate = "01/01/1950",
                KeyPlayerDeceasedFlag = "N",
                KeyPlayerCreatedDate = "05/08/2026 14:32:51",
            };

            Case model = CaseMapper.ToModel(siebel);

            Assert.Equal("1-5371KIQ", model.Id);
            Assert.Equal("1-11077140770", model.CaseNumber);
            Assert.Equal("Employment and Assistance", model.Type);
            Assert.Equal(new DateOnly(2026, 7, 2), model.ReopenedDate);
            Assert.Equal(new DateOnly(2027, 7, 2), model.RenewReviewDate);
            Assert.False(model.IsRestricted);
            Assert.Equal(new DateTime(2026, 6, 29, 13, 48, 10, DateTimeKind.Unspecified), model.CreatedDate);
            Assert.Equal(DateTimeKind.Unspecified, model.CreatedDate!.Value.Kind);
            Assert.Equal(new DateTime(2026, 7, 7, 10, 37, 5, DateTimeKind.Unspecified), model.LastUpdatedDate);
            Assert.Equal("1-532MU4J", model.KeyPlayerId);
            Assert.Equal("1-11069734915", model.KeyPlayerPersonId);
            Assert.Equal(new DateOnly(1950, 1, 1), model.KeyPlayerBirthDate);
            Assert.False(model.IsKeyPlayerDeceased);
            Assert.Equal(new DateTimeOffset(2026, 5, 8, 14, 32, 51, TimeSpan.Zero), model.KeyPlayerCreated);
            Assert.Null(model.ClosedDate);
            Assert.Empty(model.UnparsedValues);
            Assert.Empty(model.AdditionalFields);
        }

        [Fact]
        public void ToModel_TypesAContactRow()
        {
            SiebelCaseContact siebel = new()
            {
                Id = "1-532MU4J",
                Relationship = "Key player",
                Primary = "Y",
                StartDate = "07/02/2026 16:47:10",
                FirstName = "Winnona",
                LastName = "Peircee",
                DateofBirth = "01/01/1950",
                SIN = "771445889",
                PersonIDICM = "1-11069734915",
                PersonIDMIS = "01232317",
                BCeIDUserName = "winnona-afa-test",
                Subject = "Y",
                SubjectChild = "N",
                ParentCaregiver = "N",
                Deceased = "N",
                PotentialDuplicate = "N",
            };

            CaseContact model = CaseMapper.ToModel(siebel);

            Assert.Equal("1-532MU4J", model.Id);
            Assert.Equal("Key player", model.Relationship);
            Assert.True(model.IsPrimary);
            Assert.Equal(new DateTime(2026, 7, 2, 16, 47, 10, DateTimeKind.Unspecified), model.StartDate);
            Assert.Equal(new DateOnly(1950, 1, 1), model.BirthDate);
            Assert.Equal("771445889", model.Sin);
            Assert.Equal("1-11069734915", model.PersonId);
            Assert.Equal("01232317", model.MisPersonId);
            Assert.Equal("winnona-afa-test", model.BceidUserName);
            Assert.True(model.IsSubject);
            Assert.False(model.IsSubjectChild);
            Assert.False(model.IsParentOrCaregiver);
            Assert.False(model.IsDeceased);
            Assert.False(model.IsPotentialDuplicate);
            Assert.Empty(model.UnparsedValues);
        }

        [Fact]
        public void ToModel_KeepsWhatItCannotReadRatherThanGuessing()
        {
            Case model = CaseMapper.ToModel(new SiebelCase { ClosedDate = "sometime", RestrictedFlag = "Maybe" });

            // Null, not false: "unrestricted" is a claim, and "Maybe" does not support it.
            Assert.Null(model.ClosedDate);
            Assert.Null(model.IsRestricted);
            Assert.Equal("sometime", model.UnparsedValues["Closed Date"]);
            Assert.Equal("Maybe", model.UnparsedValues["Restricted Flag"]);
        }

        [Fact]
        public void AnUndeclaredFieldSurfacesInAdditionalFields_AndLinksDoNot()
        {
            SiebelCase siebel = JsonSerializer.Deserialize<SiebelCase>(
                """
                {
                  "Id": "1-5371KIQ",
                  "Some New Field": "value",
                  "Link": [ { "rel": "self", "href": "https://icm/x", "name": "Case" } ]
                }
                """,
                IcmRefitSettings.JsonOptions)!;

            Case model = CaseMapper.ToModel(siebel);

            Assert.Equal("value", Assert.Single(model.AdditionalFields).Value.GetString());
        }

        [Fact]
        public void ToModel_TurnsANullBodyIntoAnEmptyResult()
        {
            Assert.Empty(CaseMapper.ToModel((SiebelCaseListResponse?)null).Items);
            Assert.Empty(CaseMapper.ToModel((SiebelCaseContactListResponse?)null));
        }

        private static IEnumerable<string> Declared<T>() => typeof(T)
            .GetProperties()
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(name => name is not null and not "Link")
            .Select(name => name!)
            .Order(StringComparer.Ordinal);
    }
}
