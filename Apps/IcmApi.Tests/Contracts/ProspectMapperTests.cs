namespace Icm.Api.Tests.Contracts
{
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Icm.Api.Contracts;
    using Icm.Api.Models;

    /// <summary>
    /// The prospect mapping: the search expression the library builds and what it refuses,
    /// the row a create sends and what it insists on, and the typing of what comes back.
    /// </summary>
    public class ProspectMapperTests
    {
        private static ProspectInput SoundInput => new()
        {
            ServiceRequestId = "1-53CJXK6",
            FirstName = "Myss",
            LastName = "IntegrationTest",
            BirthDate = new DateOnly(2000, 1, 25),
            Gender = "Man/Boy",
        };

        [Fact]
        public void ToSiebel_MatchesEachCriterionExactly_AndJoinsThemWithAnd()
        {
            SiebelListQuery query = ProspectMapper.ToSiebel(new ProspectQuery
            {
                Id = "1-53CJXKH",
                ServiceRequestId = "1-53CJXK6",
                ProspectiveContactUId = "abc",
                Source = "MySS",
            });

            Assert.Equal(
                "[Id] = \"1-53CJXKH\""
                + " AND [Service Request Id] = \"1-53CJXK6\""
                + " AND [Prospective Contact UId] = \"abc\""
                + " AND [Source] = \"MySS\"",
                query.SearchSpec);
            Assert.Equal(SiebelFlag.Yes, query.UniformResponse);
            Assert.Equal("None", query.ChildLinks);
            Assert.Null(query.Fields);
        }

        [Fact]
        public void ASearchWithNoCriterionIsRefused()
        {
            Assert.Throws<ArgumentException>(() => ProspectMapper.ToSiebel(
                new ProspectQuery { PageSize = 100, ViewMode = "Manager", IncludeTotalCount = true }));
            Assert.Throws<ArgumentNullException>(() => ProspectMapper.ToSiebel((ProspectQuery)null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("x\" OR [Last Name] LIKE \"*")]
        [InlineData("abc*")]
        [InlineData("a?c")]
        [InlineData("abc\ndef")]
        public void AValueThatIsBlankOrCouldRewriteTheSearchIsRefused_OnEveryTextCriterion(string hostile)
        {
            PropertyInfo[] criteria = [.. typeof(ProspectQuery).GetProperties()
                .Where(property => property.PropertyType == typeof(string)
                    && property.Name != nameof(ProspectQuery.ViewMode))];
            Assert.Equal(4, criteria.Length);

            foreach (PropertyInfo criterion in criteria)
            {
                ProspectQuery query = new();
                criterion.SetValue(query, hostile);

                ArgumentException exception = Assert.Throws<ArgumentException>(() => ProspectMapper.ToSiebel(query));
                Assert.Equal(criterion.Name, exception.ParamName);
                Assert.DoesNotContain("abc", exception.Message, StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void TheVisibilityModeDefaultsToOrganization_NotToIcms(string? viewMode)
        {
            Assert.Equal("Organization", ProspectMapper.ToSiebel(new ProspectQuery { Id = "1-X", ViewMode = viewMode }).ViewMode);
            Assert.Equal("Organization", ProspectMapper.ToReadSiebel(new ProspectReadOptions { ViewMode = viewMode }).ViewMode);
            Assert.Equal("Organization", ProspectMapper.ToChildSiebel(new ProspectReadOptions { ViewMode = viewMode }).ViewMode);
            Assert.Equal("Organization", ProspectMapper.ToReadSiebel(null).ViewMode);
            Assert.Equal("Organization", ProspectMapper.ToChildSiebel(null).ViewMode);
        }

        [Fact]
        public void ToChildSiebel_AsksForAFullPageFromTheRowItIsGiven()
        {
            SiebelListQuery first = ProspectMapper.ToChildSiebel(null);
            SiebelListQuery next = ProspectMapper.ToChildSiebel(null, startRowNum: 200);

            Assert.Equal(100, first.PageSize);
            Assert.Equal(0, first.StartRowNum);
            Assert.Equal(200, next.StartRowNum);
            Assert.Null(first.SearchSpec);
            Assert.Null(first.Fields);
            Assert.Throws<ArgumentOutOfRangeException>(() => ProspectMapper.ToChildSiebel(null, startRowNum: -1));
        }

        [Fact]
        public void ToSiebel_WritesACreateInSiebelsShapes_WithThePlaceholderKey_AndOnlyWhatIsSet()
        {
            ProspectInput input = SoundInput;
            input.IsApplicant = true;
            input.Province = "BC";
            input.Sin = "046454286";
            input.Purpose = "Residence/Mailing";

            SiebelProspect row = ProspectMapper.ToSiebel(input);

            // ICM's documented placeholder, which Siebel replaces (MEASURED 2026-09-29).
            Assert.Equal("NULL", row.Id);
            Assert.Equal("1-53CJXK6", row.ServiceRequestId);
            Assert.Equal("01/25/2000", row.BirthDate);
            Assert.Equal("Man/Boy", row.MF);
            Assert.Equal("Y", row.ApplicantFlag);
            Assert.Equal("BC", row.State);
            Assert.Equal("046454286", row.SocialSecurityNumber);
            Assert.Null(row.Created);
            Assert.Null(row.CreatedBy);
            Assert.Null(row.HomePhone);

            string json = JsonSerializer.Serialize(row, IcmRefitSettings.JsonOptions);
            Assert.DoesNotContain("Created", json, StringComparison.Ordinal);
            Assert.DoesNotContain("Last Updated", json, StringComparison.Ordinal);
            Assert.DoesNotContain("null", json, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryWritableFieldOfTheInputReachesTheRow()
        {
            // Each string property of the input, set to its own name, lands somewhere on
            // the row — so a property added to one and not the other cannot go missing.
            ProspectInput input = SoundInput;
            PropertyInfo[] texts = [.. typeof(ProspectInput).GetProperties().Where(p => p.PropertyType == typeof(string))];
            foreach (PropertyInfo text in texts)
            {
                text.SetValue(input, text.Name);
            }

            SiebelProspect row = ProspectMapper.ToSiebel(input);
            string[] sent = [.. typeof(SiebelProspect).GetProperties()
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => (string?)p.GetValue(row))
                .Where(v => v is not null)
                .Select(v => v!)];

            // Every text plus the formatted birth date and the placeholder key.
            Assert.Equal(texts.Length + 2, sent.Length);
            Assert.Equal(ProspectMapper.CreatePlaceholderId, row.Id);
            Assert.All(texts, text => Assert.Contains(text.Name, sent));
        }

        [Fact]
        public void ACreateWithoutARequiredFieldOrAServiceRequestIsRefused_BeforeAnythingIsBuilt()
        {
            Assert.Throws<ArgumentNullException>(() => ProspectMapper.ToSiebel((ProspectInput)null!));

            // ICM's field list (ADO 8249) marks the names, the birth date and the gender required.
            foreach ((string property, Action<ProspectInput> blank) in new (string, Action<ProspectInput>)[]
            {
                (nameof(ProspectInput.FirstName), i => i.FirstName = null),
                (nameof(ProspectInput.LastName), i => i.LastName = " "),
                (nameof(ProspectInput.BirthDate), i => i.BirthDate = null),
                (nameof(ProspectInput.Gender), i => i.Gender = string.Empty),
                (nameof(ProspectInput.ServiceRequestId), i => i.ServiceRequestId = null),
            })
            {
                ProspectInput input = SoundInput;
                blank(input);
                ArgumentException exception = Assert.Throws<ArgumentException>(() => ProspectMapper.ToSiebel(input));
                Assert.Equal(property, exception.ParamName);
            }
        }

        [Fact]
        public void ANestedRowSendsNoKeyAndNoParent_AndRefusesAParent()
        {
            ProspectInput input = SoundInput;
            input.ServiceRequestId = null;

            SiebelProspect nested = ProspectMapper.ToNestedSiebel(input);
            Assert.Null(nested.Id);
            Assert.Null(nested.ServiceRequestId);
            Assert.Equal("Myss", nested.FirstName);

            Assert.Throws<ArgumentException>(() => ProspectMapper.ToNestedSiebel(SoundInput));
        }

        [Fact]
        public void Validate_RefusesExactlyWhatToSiebelRefuses()
        {
            Assert.Throws<ArgumentNullException>(() => ProspectMapper.Validate((ProspectQuery)null!));
            Assert.Throws<ArgumentException>(() => ProspectMapper.Validate(new ProspectQuery()));
            Assert.Throws<ArgumentException>(() => ProspectMapper.Validate(new ProspectQuery { Source = "x\" OR [Id] LIKE \"*" }));
            ProspectMapper.Validate(new ProspectQuery { ServiceRequestId = "1-53CJXK6" });

            Assert.Throws<ArgumentNullException>(() => ProspectMapper.Validate((ProspectInput)null!));
            Assert.Throws<ArgumentException>(() => ProspectMapper.Validate(new ProspectInput { FirstName = "F", LastName = "L" }));
            ProspectMapper.Validate(SoundInput);
        }

        [Fact]
        public void ToModel_TypesWhatSiebelSendsAsText()
        {
            // The live shape of a workflow-created row, SIT2 2026-09-14 (SR 1-53CJUNB).
            SiebelProspect siebel = new()
            {
                Id = "1-53CJUNM",
                ServiceRequestId = "1-53CJUNB",
                FirstName = "Myss",
                LastName = "IntegrationTest",
                BirthDate = "01/01/1950",
                MF = string.Empty,
                SocialSecurityNumber = "046454286",
                ApplicantFlag = "N",
                Purpose = "Residence/Mailing",
                StreetAddress = "501 Belleville St",
                City = "Victoria",
                State = "BC",
                PostalCode = "V8V 1X4",
                HomePhone = "2505550199",
                PreferredCommunicationMethod = "Home Phone",
                Created = "09/14/2026 12:07:36",
                CreatedBy = "1-A6IS",
                CreatedByName = "SIEBEL_EAI",
                LastUpdated = "09/14/2026 12:07:36",
                LastUpdatedBy = "1-A6IS",
                LastUpdatedByName = "SIEBEL_EAI",
            };

            Prospect model = ProspectMapper.ToModel(siebel);

            Assert.Equal("1-53CJUNM", model.Id);
            Assert.Equal("1-53CJUNB", model.ServiceRequestId);
            Assert.Equal(new DateOnly(1950, 1, 1), model.BirthDate);
            Assert.Equal(string.Empty, model.Gender);
            Assert.Equal("046454286", model.Sin);
            Assert.False(model.IsApplicant);
            Assert.Equal("BC", model.Province);
            Assert.Equal(new DateTime(2026, 9, 14, 12, 7, 36, DateTimeKind.Unspecified), model.Created);
            Assert.Equal(DateTimeKind.Unspecified, model.Created!.Value.Kind);
            Assert.Equal("1-A6IS", model.CreatedById);
            Assert.Equal("SIEBEL_EAI", model.CreatedBy);
            Assert.Equal("SIEBEL_EAI", model.LastUpdatedBy);
            Assert.Null(model.Alias);
            Assert.Empty(model.UnparsedValues);
            Assert.Empty(model.AdditionalFields);
        }

        [Fact]
        public void ToModel_KeepsWhatItCannotReadRatherThanGuessing()
        {
            Prospect model = ProspectMapper.ToModel(new SiebelProspect { BirthDate = "sometime", ApplicantFlag = "Maybe" });

            Assert.Null(model.BirthDate);
            Assert.Null(model.IsApplicant);
            Assert.Equal("sometime", model.UnparsedValues["Birth Date"]);
            Assert.Equal("Maybe", model.UnparsedValues["Applicant Flag"]);
        }

        [Fact]
        public void AnUndeclaredFieldSurfacesInAdditionalFields_AndLinksDoNot_InEitherShape()
        {
            SiebelProspect read = JsonSerializer.Deserialize<SiebelProspect>(
                """
                {
                  "Id": "1-53CJUNM",
                  "Some New Field": "value",
                  "Link": [ { "rel": "parent", "href": "https://icm/x", "name": "ServiceRequest" } ]
                }
                """,
                IcmRefitSettings.JsonOptions)!;
            Assert.Equal("value", Assert.Single(ProspectMapper.ToModel(read).AdditionalFields).Value.GetString());

            // A write's answer carries Link as one object (MEASURED 2026-09-29).
            SiebelProspect written = JsonSerializer.Deserialize<SiebelProspect>(
                """{ "Id": "1-53MU3XZ", "Link": { "rel": "self", "href": "https://icm/x", "name": "SRProspects" } }""",
                IcmRefitSettings.JsonOptions)!;
            Assert.Equal("self", Assert.Single(written.Link!).Rel);
            Assert.Empty(ProspectMapper.ToModel(written).AdditionalFields);
        }

        [Fact]
        public void TheWireContractDeclaresEveryFieldOfTheDocument_PlusTheLiveId()
        {
            string[] declared = [.. typeof(SiebelProspect).GetProperties()
                .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
                .Where(name => name is not null and not "Link")
                .Select(name => name!)
                .Order(StringComparer.Ordinal)];

            string[] document =
            [
                "Address Care Of", "Alias", "Alternate Phone #", "Applicant Flag", "Birth Date", "Cellular Phone #", "City",
                "Comment", "Created", "Created By", "Created By Name", "Email Address", "First Name", "Home Phone #",
                "ICM BCSC DID", "Last Name", "Last Updated", "Last Updated By", "Last Updated By Name", "M/F", "M/M",
                "Marital Status Code", "Middle Name", "Portal Role", "Postal Code", "Preferred Communication Method",
                "Previous First Name", "Previous Last Name", "Previous Middle Name", "Prospective Contact UId", "Purpose",
                "Service Request Id", "Social Security Number", "Source", "Source Name", "Source Type", "State",
                "Street Address", "Street Address 2", "Street Address 3", "Unit #", "Work Phone #",
            ];

            Assert.Equal(42, document.Length);
            Assert.Equal(document.Append("Id").Order(StringComparer.Ordinal), declared);
        }

        [Fact]
        public void ToModel_TurnsANullBodyIntoAnEmptyResult()
        {
            Assert.Empty(ProspectMapper.ToModel((SiebelProspectListResponse?)null).Items);
            Assert.Empty(ProspectMapper.ToModels(null));
        }
    }
}
