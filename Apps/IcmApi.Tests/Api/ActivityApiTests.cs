namespace Icm.Api.Tests.Api
{
    using System.Net;
    using System.Text.Json;
    using Icm.Api;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// Checks the requests <see cref="IActivityApi"/> actually puts on the wire against
    /// <c>docs/integration/Activities_OpenApi.json</c>, <c>ActivityPlan_OpenApi.json</c> and
    /// the measured child-collection paths.
    /// </summary>
    public class ActivityApiTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static (IActivityApi Api, RecordingHttpMessageHandler Handler) CreateApi()
        {
            RecordingHttpMessageHandler handler = new(HttpStatusCode.OK, "{}");
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (RestService.For<IActivityApi>(httpClient, IcmRefitSettings.Create()), handler);
        }

        private static string Parameter(RecordingHttpMessageHandler handler, string name)
        {
            string sent = handler.Request!.RequestUri!.AbsoluteUri;
            int start = sent.IndexOf(name + "=", StringComparison.Ordinal);
            Assert.True(start >= 0, $"{name} was not sent: {sent}");
            return Uri.UnescapeDataString(sent[start..].Split('&')[0]);
        }

        [Fact]
        public async Task TheReadsUseTheDocumentedAndMeasuredPaths()
        {
            (IActivityApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, new SiebelListQuery());
            Assert.Equal(HttpMethod.Get, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Activities/Activities/", handler.Request.RequestUri!.AbsolutePath);

            await api.GetAsync("t", null, "1-53CJXOB", new SiebelItemQuery());
            Assert.Equal("/gov/v1.0/data/Activities/Activities/1-53CJXOB/", handler.Request!.RequestUri!.AbsolutePath);

            await api.GetForServiceRequestAsync("t", null, "1-53CJXO6", new SiebelListQuery());
            Assert.Equal("/gov/v1.0/data/ServiceRequest/ServiceRequest/1-53CJXO6/Activities/", handler.Request!.RequestUri!.AbsolutePath);

            await api.SearchPlansAsync("t", null, new SiebelListQuery());
            Assert.Equal("/gov/v1.0/data/ActivityPlan/ActivityPlan/", handler.Request!.RequestUri!.AbsolutePath);

            await api.GetPlansForServiceRequestAsync("t", null, "1-53CJXO6", new SiebelListQuery());
            Assert.Equal("/gov/v1.0/data/ServiceRequest/ServiceRequest/1-53CJXO6/ActivityPlan/", handler.Request!.RequestUri!.AbsolutePath);
            Assert.Equal(HttpMethod.Get, handler.Request.Method);
        }

        [Fact]
        public async Task UpsertAsync_PutsToTheStandalonePath_SendingOnlyTheSetFields()
        {
            (IActivityApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.UpsertAsync("t", null, new SiebelActivity
            {
                Id = "NULL", ActivitySRId = "1-53MU3X7", Type = "Task", ActionBy = "Staff", Due = "10/29/2026 00:00:00",
            });

            Assert.Equal(HttpMethod.Put, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Activities/Activities/", handler.Request.RequestUri!.AbsolutePath);

            using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
            Assert.Equal("NULL", body.RootElement.GetProperty("Id").GetString());
            Assert.Equal("1-53MU3X7", body.RootElement.GetProperty("Activity SR Id").GetString());
            Assert.Equal("10/29/2026 00:00:00", body.RootElement.GetProperty("Due").GetString());
            Assert.Equal(5, body.RootElement.EnumerateObject().Count());
        }

        [Fact]
        public void ThereIsNoWriteToAPlan_OrToAChildPath()
        {
            HttpMethodAttribute[] writes = [.. typeof(IActivityApi).GetMethods()
                .SelectMany(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>())
                .Where(attribute => attribute.Method != HttpMethod.Get)];

            HttpMethodAttribute only = Assert.Single(writes);
            Assert.Equal("/data/Activities/Activities/", only.Path);
        }

        [Fact]
        public async Task EveryCallIdentifiesItselfTheSameTwoWays()
        {
            (IActivityApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetForServiceRequestAsync("token-abc", "IDIR\\SOMEONE", "1-53CJXO6", new SiebelListQuery());

            Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
            Assert.Equal("token-abc", handler.Request.Headers.Authorization.Parameter);
            Assert.Equal("IDIR\\SOMEONE", Assert.Single(handler.Request.Headers.GetValues(IServiceRequestApi.TrustedUserNameHeader)));
        }

        [Fact]
        public async Task TheSearchesTravelAsTheMapperBuiltThem()
        {
            (IActivityApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, ActivityMapper.ToSiebel(new ActivityQuery { ServiceRequestId = "1-53CJXO6", PlanName = "Bus Pass - New Intake" }));
            Assert.Equal("searchspec=[Activity SR Id] = \"1-53CJXO6\" AND [Plan Name] = \"Bus Pass - New Intake\"", Parameter(handler, "searchspec"));
            Assert.Equal("ViewMode=Organization", Parameter(handler, "ViewMode"));
            Assert.Equal("childlinks=None", Parameter(handler, "childlinks"));
            Assert.DoesNotContain("fields=", handler.Request!.RequestUri!.AbsoluteUri, StringComparison.Ordinal);

            await api.SearchPlansAsync("t", null, ActivityMapper.ToSiebel(new ActivityPlanQuery { TemplateId = "1-30LDVGD", Status = "In Progress" }));
            Assert.Equal("searchspec=[Template Id] = \"1-30LDVGD\" AND [Status] = \"In Progress\"", Parameter(handler, "searchspec"));

            await api.GetForServiceRequestAsync("t", null, "1-53CJXO6", ActivityMapper.ToChildSiebel(new ActivityReadOptions { ViewMode = "Manager" }, 200));
            Assert.Equal("PageSize=100", Parameter(handler, "PageSize"));
            Assert.Equal("StartRowNum=200", Parameter(handler, "StartRowNum"));
            Assert.Equal("ViewMode=Manager", Parameter(handler, "ViewMode"));
            Assert.DoesNotContain("fields=", handler.Request!.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        }
    }
}
