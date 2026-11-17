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
    /// Checks the requests <see cref="IProspectApi"/> actually puts on the wire against
    /// <c>docs/integration/SRProspects_OpenApi.json</c> and the measured child-collection path.
    /// </summary>
    public class ProspectApiTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static (IProspectApi Api, RecordingHttpMessageHandler Handler) CreateApi()
        {
            RecordingHttpMessageHandler handler = new(HttpStatusCode.OK, "{}");
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (RestService.For<IProspectApi>(httpClient, IcmRefitSettings.Create()), handler);
        }

        private static string Parameter(RecordingHttpMessageHandler handler, string name)
        {
            string sent = handler.Request!.RequestUri!.AbsoluteUri;
            int start = sent.IndexOf(name + "=", StringComparison.Ordinal);
            Assert.True(start >= 0, $"{name} was not sent: {sent}");
            return Uri.UnescapeDataString(sent[start..].Split('&')[0]);
        }

        [Fact]
        public async Task SearchAsync_KeepsTheStandalonePathAndItsTrailingSlash()
        {
            (IProspectApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, new SiebelListQuery());

            Assert.Equal(HttpMethod.Get, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Prospects/SRProspects/", handler.Request.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task GetAsync_PutsTheKeyInThePath()
        {
            (IProspectApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetAsync("t", null, "1-53CJXKH", new SiebelItemQuery());

            Assert.Equal(HttpMethod.Get, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Prospects/SRProspects/1-53CJXKH/", handler.Request.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task GetForServiceRequestAsync_ReadsTheChildOfTheServiceRequest()
        {
            (IProspectApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetForServiceRequestAsync("t", null, "1-53CJXK6", new SiebelListQuery());

            Assert.Equal(HttpMethod.Get, handler.Request!.Method);
            Assert.Equal(
                "/gov/v1.0/data/ServiceRequest/ServiceRequest/1-53CJXK6/SRProspects/",
                handler.Request.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task UpsertAsync_PutsToTheStandalonePath_SendingOnlyTheSetFields()
        {
            // A PUT, not a POST: MEASURED 2026-09-29, the same body by POST is refused.
            (IProspectApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.UpsertAsync("t", null, new SiebelProspect
            {
                Id = "NULL", ServiceRequestId = "1-53CJXK6", FirstName = "Myss", LastName = "IntegrationTest", BirthDate = "01/25/2000",
            });

            Assert.Equal(HttpMethod.Put, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Prospects/SRProspects/", handler.Request.RequestUri!.AbsolutePath);
            Assert.DoesNotContain("excludeEmptyFieldsInResponse", handler.Request.RequestUri.AbsoluteUri, StringComparison.Ordinal);

            using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
            Assert.Equal("NULL", body.RootElement.GetProperty("Id").GetString());
            Assert.Equal("1-53CJXK6", body.RootElement.GetProperty("Service Request Id").GetString());
            Assert.Equal("01/25/2000", body.RootElement.GetProperty("Birth Date").GetString());
            Assert.Equal(5, body.RootElement.EnumerateObject().Count());
        }

        [Fact]
        public void ThereIsNoWriteToTheChildPath()
        {
            // A POST to …/ServiceRequest/{key}/SRProspects/ makes a new service request
            // (MEASURED 2026-09-29); the interface must not offer it.
            Assert.All(
                typeof(IProspectApi).GetMethods(),
                method => Assert.DoesNotContain(
                    method.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>(),
                    attribute => attribute.Method != HttpMethod.Get && attribute.Path.Contains("/ServiceRequest/", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task EveryCallIdentifiesItselfTheSameTwoWays()
        {
            (IProspectApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetForServiceRequestAsync("token-abc", "IDIR\\SOMEONE", "1-53CJXK6", new SiebelListQuery());

            Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
            Assert.Equal("token-abc", handler.Request.Headers.Authorization.Parameter);
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request.Headers.GetValues(IServiceRequestApi.TrustedUserNameHeader)));
        }

        [Fact]
        public async Task TheSearchTravelsAsTheMapperBuiltIt()
        {
            (IProspectApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, ProspectMapper.ToSiebel(new ProspectQuery { ServiceRequestId = "1-53CJXK6" }));

            Assert.Equal("searchspec=[Service Request Id] = \"1-53CJXK6\"", Parameter(handler, "searchspec"));
            Assert.Equal("ViewMode=Organization", Parameter(handler, "ViewMode"));
            Assert.Equal("childlinks=None", Parameter(handler, "childlinks"));
            Assert.Equal("uniformresponse=Y", Parameter(handler, "uniformresponse"));
            Assert.DoesNotContain("fields=", handler.Request!.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TheChildReadPagesFromTheRowItIsGiven()
        {
            (IProspectApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetForServiceRequestAsync(
                "t", null, "1-53CJXK6", ProspectMapper.ToChildSiebel(new ProspectReadOptions { ViewMode = "Manager" }, 200));

            Assert.Equal("PageSize=100", Parameter(handler, "PageSize"));
            Assert.Equal("StartRowNum=200", Parameter(handler, "StartRowNum"));
            Assert.Equal("ViewMode=Manager", Parameter(handler, "ViewMode"));
            Assert.DoesNotContain("searchspec", handler.Request!.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        }
    }
}
