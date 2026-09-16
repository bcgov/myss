namespace Myss.Api.Tests.Contracts
{
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Myss.Api.Models;
    using Xunit;

    /// <summary>
    /// The body this API posts to the ICM middleware is the shared sample in
    /// <c>Shared/contracts/bus-pass-application.sample.json</c>, byte for byte in
    /// meaning. The middleware's own suite reads the same file, so a change on
    /// either side that the other does not follow is a failing test.
    /// </summary>
    public class BusPassMiddlewareContractTests
    {
        // The same options IcmApiBusPassSubmissionProvider serializes with.
        private static readonly JsonSerializerOptions ProviderOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public async Task TheProviderBody_MatchesTheSharedSample()
        {
            var application = new BusPassApplicationModel
            {
                RequestType = BusPassRequestType.AddressUpdate,
                ApplicantType = BusPassApplicantType.Over65,
                AcknowledgedPassCancellation = false,
                AcknowledgedEligibilityCriteria = true,
                SocialInsuranceNumber = "046454286",
                BusPassAccountNumber = "12345678",
                FirstName = "Myss",
                LastName = "IntegrationTest",
                DateOfBirth = new DateOnly(1950, 1, 1),
                PhoneNumber = "2505550199",
                PhoneType = BusPassPhoneType.Home,
                LeaveMessageAllowed = true,
                EmailAddress = "myss.integrationtest@example.com",
                PreferredContactMethod = BusPassContactMethod.Email,
                ResidentialAddress = new BusPassAddressModel
                {
                    Unit = "1",
                    Line1 = "501 Belleville St",
                    Line2 = "Rear entrance",
                    City = "Victoria",
                    Province = "BC",
                    PostalCode = "V8V 1X4",
                },
                MailingAddress = new BusPassAddressModel
                {
                    Unit = "2",
                    Line1 = "PO Box 9041 Stn Prov Govt",
                    Line2 = "Parliament Buildings",
                    City = "Victoria",
                    Province = "BC",
                    PostalCode = "V8W 9E1",
                },
            };

            JsonNode? actual = JsonNode.Parse(JsonSerializer.Serialize(application, ProviderOptions));
            JsonNode? expected = JsonNode.Parse(await File.ReadAllTextAsync("bus-pass-application.sample.json"));
            Assert.NotNull(expected);

            // The "//" note is for readers of the file, not part of the contract.
            expected.AsObject().Remove("//");

            Assert.True(
                JsonNode.DeepEquals(expected, actual),
                $"The provider body differs from the shared sample.{Environment.NewLine}Expected: {expected.ToJsonString()}{Environment.NewLine}Actual:   {actual?.ToJsonString()}");
        }
    }
}
