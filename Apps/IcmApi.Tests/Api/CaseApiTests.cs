namespace Icm.Api.Tests.Api
{
    using System.Net;
    using Icm.Api;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// Checks the requests <see cref="ICaseApi"/> actually puts on the wire against
    /// <c>docs/integration/Case_OpenApi.json</c> and the measured child-collection path.
    /// </summary>
    public class CaseApiTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static (ICaseApi Api, RecordingHttpMessageHandler Handler) CreateApi()
        {
            RecordingHttpMessageHandler handler = new(HttpStatusCode.OK, "{}");
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (RestService.For<ICaseApi>(httpClient, IcmRefitSettings.Create()), handler);
        }

        private static string Parameter(RecordingHttpMessageHandler handler, string name)
        {
            // AbsoluteUri, not Query or ToString(): those can show an unescaped form that
            // is not what goes on the wire.
            string sent = handler.Request!.RequestUri!.AbsoluteUri;
            int start = sent.IndexOf(name + "=", StringComparison.Ordinal);
            Assert.True(start >= 0, $"{name} was not sent: {sent}");
            return Uri.UnescapeDataString(sent[start..].Split('&')[0]);
        }

        [Fact]
        public async Task SearchAsync_KeepsTheCollectionPathAndItsTrailingSlash()
        {
            (ICaseApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, new SiebelListQuery());

            Assert.Equal(HttpMethod.Get, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Cases/Case/", handler.Request.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task GetAsync_PutsTheKeyInThePath()
        {
            (ICaseApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetAsync("t", null, "1-5371KIQ", new SiebelItemQuery());

            Assert.Equal(HttpMethod.Get, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/Cases/Case/1-5371KIQ/", handler.Request.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task GetContactsAsync_ReadsTheContactChildOfTheCase()
        {
            (ICaseApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetContactsAsync("t", null, "1-5371KIQ", new SiebelListQuery());

            Assert.Equal("/gov/v1.0/data/Cases/Case/1-5371KIQ/Contact/", handler.Request!.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task EveryCallIdentifiesItselfTheSameTwoWays()
        {
            (ICaseApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetContactsAsync("token-abc", "IDIR\\SOMEONE", "1-5371KIQ", new SiebelListQuery());

            Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
            Assert.Equal("token-abc", handler.Request.Headers.Authorization.Parameter);
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request.Headers.GetValues(IServiceRequestApi.TrustedUserNameHeader)));
        }

        [Fact]
        public async Task TheSearchTravelsAsTheMapperBuiltIt()
        {
            (ICaseApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, CaseMapper.ToSiebel(new CaseQuery
            {
                CaseNumber = "1-11077140770", KeyPlayerContactId = "1-532MU4J",
            }));

            Assert.Equal(
                "searchspec=[Case Num] = \"1-11077140770\" AND [Key Player Id] = \"1-532MU4J\"",
                Parameter(handler, "searchspec"));
            Assert.Equal("ViewMode=Manager", Parameter(handler, "ViewMode"));
            Assert.Equal("childlinks=None", Parameter(handler, "childlinks"));
            Assert.Equal("uniformresponse=Y", Parameter(handler, "uniformresponse"));
        }

        [Fact]
        public async Task TheFieldListsTravelWithTheirAwkwardNamesIntact()
        {
            // "Key Player M/F": a slash in a query value, which the gateway is happy with
            // (it is only a %2F in a *path* it answers 400 to).
            (ICaseApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.GetAsync("t", null, "1-5371KIQ", CaseMapper.ToReadSiebel(null));
            Assert.Equal($"fields={string.Join(',', CaseMapper.RequestedFields)}", Parameter(handler, "fields"));
            Assert.Equal("ViewMode=Manager", Parameter(handler, "ViewMode"));

            await api.GetContactsAsync("t", null, "1-5371KIQ", CaseMapper.ToContactsSiebel(new CaseReadOptions { ViewMode = "Group" }));
            Assert.Equal($"fields={string.Join(',', CaseMapper.RequestedContactFields)}", Parameter(handler, "fields"));
            Assert.Equal("ViewMode=Group", Parameter(handler, "ViewMode"));
            Assert.Equal("uniformresponse=Y", Parameter(handler, "uniformresponse"));
            Assert.DoesNotContain("searchspec", handler.Request!.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        }
    }
}
