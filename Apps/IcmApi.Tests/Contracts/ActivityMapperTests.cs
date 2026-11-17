namespace Icm.Api.Tests.Contracts
{
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Icm.Api.Contracts;
    using Icm.Api.Models;

    /// <summary>The activity mapping: searches and what they refuse, the row a create sends, and the typing of what comes back.</summary>
    public class ActivityMapperTests
    {
        private static ActivityInput SoundInput => new()
        {
            ServiceRequestId = "1-53MU3X7", Type = "Task", ActionBy = "Staff", ActionCode = "Validate Address and Promote Prospect",
        };

        [Fact]
        public void ToSiebel_MatchesEachCriterionExactly_AndJoinsThemWithAnd()
        {
            SiebelListQuery activities = ActivityMapper.ToSiebel(new ActivityQuery
            {
                Id = "1-A", ServiceRequestId = "1-S", CaseId = "1-C", PlanName = "Bus Pass - New Intake", Status = "Open", Type = "Task",
            });
            Assert.Equal(
                "[Id] = \"1-A\" AND [Activity SR Id] = \"1-S\" AND [Case Id] = \"1-C\" AND [Plan Name] = \"Bus Pass - New Intake\""
                + " AND [Status] = \"Open\" AND [Type] = \"Task\"",
                activities.SearchSpec);
            Assert.Null(activities.Fields);

            SiebelListQuery plans = ActivityMapper.ToSiebel(new ActivityPlanQuery
            {
                Id = "1-P", ServiceRequestId = "1-S", Template = "Bus Pass - New Intake", TemplateId = "1-30LDVGD", Status = "Closed",
            });
            Assert.Equal(
                "[Id] = \"1-P\" AND [Activity SR Id] = \"1-S\" AND [Template] = \"Bus Pass - New Intake\" AND [Template Id] = \"1-30LDVGD\""
                + " AND [Status] = \"Closed\"",
                plans.SearchSpec);
        }

        [Theory]
        [InlineData("")]
        [InlineData("x\" OR [Status] LIKE \"*")]
        [InlineData("abc*")]
        public void AValueThatIsBlankOrCouldRewriteTheSearchIsRefused_OnEveryTextCriterion(string hostile)
        {
            foreach (Type queryType in new[] { typeof(ActivityQuery), typeof(ActivityPlanQuery) })
            {
                PropertyInfo[] criteria = [.. queryType.GetProperties()
                    .Where(p => p.PropertyType == typeof(string) && p.Name != "ViewMode")];
                Assert.NotEmpty(criteria);

                foreach (PropertyInfo criterion in criteria)
                {
                    object query = Activator.CreateInstance(queryType)!;
                    criterion.SetValue(query, hostile);
                    ArgumentException exception = query is ActivityQuery a
                        ? Assert.Throws<ArgumentException>(() => ActivityMapper.ToSiebel(a))
                        : Assert.Throws<ArgumentException>(() => ActivityMapper.ToSiebel((ActivityPlanQuery)query));
                    Assert.Equal(criterion.Name, exception.ParamName);
                    Assert.DoesNotContain("abc", exception.Message, StringComparison.Ordinal);
                }
            }

            Assert.Throws<ArgumentException>(() => ActivityMapper.ToSiebel(new ActivityQuery { PageSize = 5 }));
            Assert.Throws<ArgumentException>(() => ActivityMapper.ToSiebel(new ActivityPlanQuery()));
            Assert.Throws<ArgumentNullException>(() => ActivityMapper.ToSiebel((ActivityQuery)null!));
        }

        [Fact]
        public void TheVisibilityModeDefaultsToOrganization_AndTheChildReadPages()
        {
            Assert.Equal("Organization", ActivityMapper.ToSiebel(new ActivityQuery { Id = "1-X" }).ViewMode);
            Assert.Equal("Organization", ActivityMapper.ToReadSiebel(null).ViewMode);
            Assert.Equal("Organization", ActivityMapper.ToChildSiebel(new ActivityReadOptions { ViewMode = " " }).ViewMode);
            Assert.Equal("Group", ActivityMapper.ToChildSiebel(new ActivityReadOptions { ViewMode = "Group" }).ViewMode);

            SiebelListQuery next = ActivityMapper.ToChildSiebel(null, startRowNum: 200);
            Assert.Equal(100, next.PageSize);
            Assert.Equal(200, next.StartRowNum);
            Assert.Null(next.Fields);
            Assert.Throws<ArgumentOutOfRangeException>(() => ActivityMapper.ToChildSiebel(null, -1));
        }

        [Fact]
        public void ToSiebel_WritesACreateWithThePlaceholderKeyAndTheTwoDefaults()
        {
            ActivityInput input = SoundInput;
            input.Planned = new DateTime(2026, 9, 29, 13, 30, 0);
            input.Due = new DateTime(2026, 10, 29, 0, 0, 0);
            input.IsDone = false;

            SiebelActivity row = ActivityMapper.ToSiebel(input);

            Assert.Equal("NULL", row.Id);
            Assert.Equal("1-53MU3X7", row.ActivitySRId);
            Assert.Equal("General", row.Category);
            Assert.Equal("Calendar and Activities", row.Display);
            Assert.Equal("09/29/2026 13:30:00", row.Planned);
            Assert.Equal("10/29/2026 00:00:00", row.Due);
            Assert.Equal("N", row.DoneFlag);
            Assert.Null(row.PlanName);
            Assert.Null(row.Created);

            string json = JsonSerializer.Serialize(row, IcmRefitSettings.JsonOptions);
            Assert.DoesNotContain("null", json, StringComparison.Ordinal);
            Assert.DoesNotContain("Plan Name", json, StringComparison.Ordinal);

            // A caller's own category is kept.
            input.Category = "Other";
            Assert.Equal("Other", ActivityMapper.ToSiebel(input).Category);
        }

        [Fact]
        public void ACreateNeedsATypeAnActorAndSomethingToBeProfiledTo()
        {
            Assert.Throws<ArgumentNullException>(() => ActivityMapper.ToSiebel((ActivityInput)null!));

            ActivityInput noType = SoundInput;
            noType.Type = " ";
            Assert.Equal(nameof(ActivityInput.Type), Assert.Throws<ArgumentException>(() => ActivityMapper.ToSiebel(noType)).ParamName);

            ActivityInput noActor = SoundInput;
            noActor.ActionBy = null;
            Assert.Equal(nameof(ActivityInput.ActionBy), Assert.Throws<ArgumentException>(() => ActivityMapper.ToSiebel(noActor)).ParamName);

            ActivityInput orphan = SoundInput;
            orphan.ServiceRequestId = null;
            Assert.Throws<ArgumentException>(() => ActivityMapper.ToSiebel(orphan));

            // Any one of the four entities will do.
            orphan.CaseId = "1-C";
            Assert.Equal("1-C", ActivityMapper.ToSiebel(orphan).CaseId);

            ActivityMapper.Validate(SoundInput);
            Assert.Throws<ArgumentException>(() => ActivityMapper.Validate(new ActivityInput()));
        }

        [Fact]
        public void ToModel_TypesWhatSiebelSendsAsText()
        {
            // The workflow's step 01 on SR 1-53CJXO6, MEASURED SIT2 2026-09-29.
            SiebelActivity siebel = new()
            {
                Id = "1-53CJXOB",
                ActivitySRId = "1-53CJXO6",
                SRNumber = "1-11086395702",
                ActivityTemplateId = "1-53CJXO9",
                PlanName = "Bus Pass - New Intake",
                Sequence = "01",
                Type = "Task",
                ActionBy = "Staff",
                ActionCode = "Validate Address and Promote Prospect",
                Status = "Open",
                DoneFlag = "N",
                Created = "09/14/2026 12:42:33",
                Due = "10/14/2026 00:00:00",
                PrimaryOwnerId = "No Match Row Id",
                Overdue = "N",
                CreatedByName = "SIEBEL_EAI",
            };

            Activity model = ActivityMapper.ToModel(siebel);

            Assert.Equal("1-53CJXOB", model.Id);
            Assert.Equal("1-53CJXO6", model.ServiceRequestId);
            Assert.Equal("1-53CJXO9", model.ActivityPlanId);
            Assert.Equal("Bus Pass - New Intake", model.PlanName);
            Assert.Equal("01", model.Sequence);
            Assert.False(model.IsDone);
            Assert.False(model.IsOverdue);
            Assert.Equal(new DateTime(2026, 9, 14, 12, 42, 33, DateTimeKind.Unspecified), model.Created);
            Assert.Equal(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Unspecified), model.Due);
            Assert.Equal("SIEBEL_EAI", model.CreatedBy);
            Assert.Null(model.Done);
            Assert.Empty(model.UnparsedValues);
            Assert.Empty(model.AdditionalFields);

            // The plan instance those steps hang off, same SR.
            ActivityPlan plan = ActivityMapper.ToModel(new SiebelActivityPlan
            {
                Id = "1-53CJXO9", ActivitySRId = "1-53CJXO6", Template = "Bus Pass - New Intake", TemplateId = "1-30LDVGD", Status = "In Progress", RestrictedFlag = "N",
            });
            Assert.Equal("1-53CJXO9", plan.Id);
            Assert.Equal("1-30LDVGD", plan.TemplateId);
            Assert.Equal("In Progress", plan.Status);
            Assert.False(plan.IsRestricted);
        }

        [Fact]
        public void ToModel_KeepsWhatItCannotRead_AndSurfacesUndeclaredFields()
        {
            Activity model = ActivityMapper.ToModel(new SiebelActivity { Due = "sometime", DoneFlag = "Maybe" });
            Assert.Null(model.Due);
            Assert.Null(model.IsDone);
            Assert.Equal("sometime", model.UnparsedValues["Due"]);
            Assert.Equal("Maybe", model.UnparsedValues["Done Flag"]);

            // The child view sends a few names the contract does not declare (Row Id,
            // Activity Id, Last Updated By Name): they arrive as data, and Link does not.
            SiebelActivity siebel = JsonSerializer.Deserialize<SiebelActivity>(
                """{ "Id": "1-53CJXOB", "Row Id": "1-53CJXOB", "Link": [ { "rel": "parent", "href": "https://icm/x", "name": "ServiceRequest" } ] }""",
                IcmRefitSettings.JsonOptions)!;
            Assert.Equal("1-53CJXOB", Assert.Single(ActivityMapper.ToModel(siebel).AdditionalFields).Value.GetString());

            // A write's answer carries Link as one object.
            SiebelActivity written = JsonSerializer.Deserialize<SiebelActivity>(
                """{ "Id": "1-53MU3Y4", "Link": { "rel": "self", "href": "https://icm/x", "name": "Activities" } }""",
                IcmRefitSettings.JsonOptions)!;
            Assert.Equal("self", Assert.Single(written.Link!).Rel);
        }

        [Fact]
        public void EveryDeclaredNameIsInTheDocument_ExceptId()
        {
            // Both contracts speak the describe documents' names, which are the live ones.
            string[] activityDocument =
            [
                "Activity SR Id", "SR Number", "SR Type", "SR Sub Type", "Case Id", "Case Number", "Incident Id", "ICM Memo Id",
                "Activity Template Id", "Plan Name", "Sequence", "Type", "ICM Type", "ICM Sub Type", "Category", "Class", "Action By",
                "Action Code", "Result Code", "Description", "Display", "Status", "Done Flag", "Done", "Due", "Planned", "Planned Completion",
                "Duration Minutes", "Priority", "Owned By", "Owned By Id", "Primary Owned By", "Primary Owner Id", "Primary Contact Id",
                "Contact First Name", "Contact Last Name", "Service Office", "ICM Service Region", "Ministry", "Ministry Id",
                "ICM Created By Office", "Restricted Flag", "Shared Flag", "Repeating", "Alarm", "Overdue", "Show In Contact Profile",
                "Created", "Created By", "Created By Name", "Updated", "Updated By", "Updated By Name",
            ];
            Assert.Equal(activityDocument.Append("Id").Order(StringComparer.Ordinal), Declared<SiebelActivity>());

            string[] planDocument =
            [
                "Id", "Activity SR Id", "Activity UID", "SR Number", "SR Type", "SR Sub Type", "Case Id", "Case #", "Case Type", "Incident Id",
                "Incident #", "Template", "Template Id", "Template Flag", "Status", "Comments", "Completed Date", "Restricted Flag",
                "Shared Flag", "ICM Created By Office", "ICM Created By Office Id", "Created By Login", "Created By Name",
            ];
            Assert.Equal(planDocument.Order(StringComparer.Ordinal), Declared<SiebelActivityPlan>());
        }

        [Fact]
        public void ToModel_TurnsANullBodyIntoAnEmptyResult()
        {
            Assert.Empty(ActivityMapper.ToModel((SiebelActivityListResponse?)null).Items);
            Assert.Empty(ActivityMapper.ToModel((SiebelActivityPlanListResponse?)null).Items);
            Assert.Empty(ActivityMapper.ToModels((SiebelActivityListResponse?)null));
            Assert.Empty(ActivityMapper.ToModels((SiebelActivityPlanListResponse?)null));
        }

        private static IEnumerable<string> Declared<T>() => typeof(T)
            .GetProperties()
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(name => name is not null and not "Link")
            .Select(name => name!)
            .Order(StringComparer.Ordinal);
    }
}
