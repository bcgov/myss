namespace Myss.Api.Tests
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Myss.Api.Controllers;
    using Myss.Api.Domain;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Account Info over real HTTP (MYSS-271): auth, the caller-only scope, the
    /// placeholder case details, and the two things a citizen may change.
    /// Phone numbers are in the 555-01xx range reserved for fiction.
    /// </summary>
    public class AccountEndpointTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private const string Account = "/v1/account";
        private const string Phones = "/v1/account/phones";
        private const string Preferences = "/v1/account/notification-preferences";

        private readonly WebApplicationFactory<Startup> _factory;

        /// <summary>Initializes a new instance of the <see cref="AccountEndpointTests"/> class.</summary>
        /// <param name="factory">The shared host factory.</param>
        public AccountEndpointTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AnAnonymousCallerIsRefused()
        {
            using IntakeTestHost host = NewHost(registered: [], mockAuth: false);

            using HttpResponseMessage response = await host.Send(null, HttpMethod.Get, Account);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AWorkerIsRefused()
        {
            using IntakeTestHost host = NewHost(registered: ["worker"]);

            using HttpResponseMessage response = await host.Send("worker", HttpMethod.Get, Account);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task ACitizenWithNoProfileIsToldToRegister()
        {
            using IntakeTestHost host = NewHost(registered: []);

            using HttpResponseMessage response = await host.Send("alice", HttpMethod.Get, Account);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using JsonDocument body = await Json(response);
            Assert.Equal(AccountController.ProfileRequiredKeyword, body.RootElement.GetProperty("keyword").GetString());
        }

        [Fact]
        public async Task ReturnsTheProfileBesideThePlaceholderCaseDetails()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);

            JsonElement account = await GetAccount(host, "alice");

            Assert.Equal(PlaceholderCaseAccountProvider.CaseNumber, account.GetProperty("caseNumber").GetString());
            Assert.Equal("alice Tester", account.GetProperty("clientName").GetString());
            Assert.Equal("alice@example.com", account.GetProperty("email").GetString());
            Assert.Equal(
                [PlaceholderCaseAccountProvider.FamilyMembers],
                Strings(account.GetProperty("familyMembers")));
            Assert.Equal(
                [PlaceholderCaseAccountProvider.MailingAddress],
                Strings(account.GetProperty("mailingAddressLines")));
            Assert.Equal(0, account.GetProperty("phones").GetArrayLength());
            Assert.False(account.GetProperty("monthlyReportReminder").GetBoolean());
        }

        [Fact]
        public async Task SavesPhoneNumbersAsDigitsInTheOrderGiven()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);

            using HttpResponseMessage response = await host.Send(
                "alice",
                HttpMethod.Put,
                Phones,
                JsonContent.Create(new
                {
                    phones = new[]
                    {
                        new { number = "(250) 555-0123", type = "Cell" },
                        new { number = "604.555.0199", type = "home" },
                    },
                }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement phones = (await GetAccount(host, "alice")).GetProperty("phones");
            Assert.Equal(2, phones.GetArrayLength());
            Assert.Equal("2505550123", phones[0].GetProperty("number").GetString());
            Assert.Equal("Cell", phones[0].GetProperty("type").GetString());
            Assert.Equal("6045550199", phones[1].GetProperty("number").GetString());
            Assert.Equal("Home", phones[1].GetProperty("type").GetString());
        }

        [Fact]
        public async Task ASecondSaveReplacesTheList()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            await PutPhones(host, "alice", new { number = "2505550123", type = "Home" }, new { number = "2505550124", type = "Work" });

            // Home is kept with a new number: the old row has to go before the
            // new one lands, or the one-per-type index would refuse it.
            using HttpResponseMessage response = await PutPhones(host, "alice", new { number = "2505550125", type = "Home" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement phones = (await GetAccount(host, "alice")).GetProperty("phones");
            Assert.Equal(1, phones.GetArrayLength());
            Assert.Equal("2505550125", phones[0].GetProperty("number").GetString());
        }

        [Fact]
        public async Task RefusesTheWholeListWithAnErrorForEveryBadField()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            await PutPhones(host, "alice", new { number = "2505550123", type = "Home" });

            using HttpResponseMessage response = await PutPhones(
                host,
                "alice",
                new { number = "555-0123", type = "Home" },
                new { number = "2505550124", type = "Pager" },
                new { number = "2505550125", type = "Home" });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            using JsonDocument body = await Json(response);
            var errors = body.RootElement.GetProperty("payload").EnumerateArray()
                .Select(e => (e.GetProperty("field").GetString(), e.GetProperty("keyword").GetString()))
                .ToList();
            Assert.Equal(
                [
                    ("phones[0].number", ValidationKeywords.PhoneInvalidFormat),
                    ("phones[1].type", ValidationKeywords.PhoneTypeUnknown),
                    ("phones[2].type", ValidationKeywords.PhoneTypeDuplicate),
                ],
                errors);

            JsonElement phones = (await GetAccount(host, "alice")).GetProperty("phones");
            Assert.Equal("2505550123", Assert.Single(phones.EnumerateArray()).GetProperty("number").GetString());
        }

        [Fact]
        public async Task AnEmptyListRemovesEveryNumber()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);
            await PutPhones(host, "alice", new { number = "2505550123", type = "Home" });

            using HttpResponseMessage response = await PutPhones(host, "alice");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await GetAccount(host, "alice")).GetProperty("phones").GetArrayLength());
        }

        [Fact]
        public async Task OneCitizensChangesNeverReachAnother()
        {
            using IntakeTestHost host = NewHost(registered: ["alice", "bob"]);

            await PutPhones(host, "alice", new { number = "2505550123", type = "Home" });

            Assert.Equal(0, (await GetAccount(host, "bob")).GetProperty("phones").GetArrayLength());
        }

        [Fact]
        public async Task SavesTheMonthlyReportReminder()
        {
            using IntakeTestHost host = NewHost(registered: ["alice"]);

            using HttpResponseMessage on = await host.Send(
                "alice", HttpMethod.Put, Preferences, JsonContent.Create(new { monthlyReportReminder = true }));

            Assert.Equal(HttpStatusCode.OK, on.StatusCode);
            Assert.True((await GetAccount(host, "alice")).GetProperty("monthlyReportReminder").GetBoolean());

            using HttpResponseMessage off = await host.Send(
                "alice", HttpMethod.Put, Preferences, JsonContent.Create(new { monthlyReportReminder = false }));

            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
            Assert.False((await GetAccount(host, "alice")).GetProperty("monthlyReportReminder").GetBoolean());
        }

        private static async Task<JsonDocument> Json(HttpResponseMessage response)
        {
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        private static string[] Strings(JsonElement array)
        {
            return [.. array.EnumerateArray().Select(e => e.GetString() ?? string.Empty)];
        }

        private static async Task<JsonElement> GetAccount(IntakeTestHost host, string persona)
        {
            using HttpResponseMessage response = await host.Send(persona, HttpMethod.Get, Account);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = await Json(response);
            return body.RootElement.GetProperty("payload").Clone();
        }

        private static Task<HttpResponseMessage> PutPhones(IntakeTestHost host, string persona, params object[] phones)
        {
            return host.Send(persona, HttpMethod.Put, Phones, JsonContent.Create(new { phones }));
        }

        private IntakeTestHost NewHost(IReadOnlyList<string> registered, bool mockAuth = true)
        {
            return new IntakeTestHost(_factory, new FakeFormSpecProvider(), registered, mockAuth);
        }
    }
}
