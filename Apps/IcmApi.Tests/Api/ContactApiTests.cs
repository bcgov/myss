namespace Icm.Api.Tests.Api
{
    using System.Net;
    using Icm.Api;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// Checks the request <see cref="IContactApi"/> actually puts on the wire against
    /// <c>docs/integration/Contact_OpenApi.json</c>.
    /// </summary>
    public class ContactApiTests
    {
        private const string Did = "MYSS+synthetic/DID0123456789abcdefghijklmno=";

        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static (IContactApi Api, RecordingHttpMessageHandler Handler) CreateApi()
        {
            RecordingHttpMessageHandler handler = new(HttpStatusCode.OK, "{}");
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (RestService.For<IContactApi>(httpClient, IcmRefitSettings.Create()), handler);
        }

        [Fact]
        public async Task SearchAsync_KeepsTheCollectionPathAndItsTrailingSlash()
        {
            (IContactApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, new SiebelListQuery());

            Assert.Equal(HttpMethod.Get, handler.Request!.Method);
            Assert.Equal("/gov/v1.0/data/ICMContact/ICMContact/", handler.Request.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task SearchAsync_IdentifiesTheCallTheSameTwoWaysEveryIcmCallDoes()
        {
            (IContactApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("token-abc", "IDIR\\SOMEONE", new SiebelListQuery());

            Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
            Assert.Equal("token-abc", handler.Request.Headers.Authorization.Parameter);
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(handler.Request.Headers.GetValues(IServiceRequestApi.TrustedUserNameHeader)));
        }

        [Fact]
        public async Task ABase64DidSurvivesTheQueryString()
        {
            // The reason this test exists: a DID is base64, and a literal + in a query
            // string is a space to the server reading it. Sent unescaped, the search would
            // return 404 for a person who is on file — indistinguishable from "no contact".
            (IContactApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync(
                "t", null, ContactMapper.ToSiebel(new ContactQuery { BcServicesCardDid = Did }));

            // AbsoluteUri, not Query or ToString(): those can show an unescaped form that
            // is not what goes on the wire.
            string sent = handler.Request!.RequestUri!.AbsoluteUri;
            string searchSpec = sent[sent.IndexOf("searchspec=", StringComparison.Ordinal)..].Split('&')[0];

            Assert.DoesNotContain("+", searchSpec, StringComparison.Ordinal);
            Assert.Contains("%2B", searchSpec, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                $"searchspec=[ICM BCSC DID] = \"{Did}\"",
                Uri.UnescapeDataString(searchSpec));
        }

        [Fact]
        public async Task TheCaseInsensitiveOperatorsSurviveTheQueryString()
        {
            (IContactApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync("t", null, ContactMapper.ToSiebel(new ContactQuery
            {
                LastName = "Int", NameMatch = ContactNameMatch.Contains, BirthDate = new DateOnly(1950, 1, 31),
            }));

            // ~ and * are both characters a URL encoder may or may not touch; what matters
            // is that the server decodes exactly the expression the mapper built.
            string sent = handler.Request!.RequestUri!.AbsoluteUri;
            string searchSpec = sent[sent.IndexOf("searchspec=", StringComparison.Ordinal)..].Split('&')[0];
            Assert.Equal(
                "searchspec=[Last Name] ~LIKE \"*Int*\" AND [Birth Date] = \"01/31/1950\"",
                Uri.UnescapeDataString(searchSpec));
        }

        [Fact]
        public async Task TheFieldListTravelsWithItsAwkwardNamesIntact()
        {
            (IContactApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SearchAsync(
                "t", null, ContactMapper.ToSiebel(new ContactQuery { BcServicesCardDid = Did }));

            // "Cellular Phone #" and "M/F": a raw # would start a fragment and silently
            // cut the rest of the query off.
            string sent = handler.Request!.RequestUri!.AbsoluteUri;
            Assert.DoesNotContain("#", sent, StringComparison.Ordinal);

            string fields = Uri.UnescapeDataString(
                sent[sent.IndexOf("fields=", StringComparison.Ordinal)..].Split('&')[0]);
            Assert.Equal($"fields={string.Join(',', ContactMapper.RequestedFields)}", fields);

            Assert.Contains("childlinks=None", sent, StringComparison.Ordinal);
            Assert.Contains("uniformresponse=Y", sent, StringComparison.Ordinal);
            Assert.DoesNotContain("PageSize", sent, StringComparison.Ordinal);
            Assert.DoesNotContain("ViewMode", sent, StringComparison.Ordinal);
        }
    }
}
