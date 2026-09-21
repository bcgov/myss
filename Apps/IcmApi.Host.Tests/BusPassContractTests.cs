namespace Icm.Api.Host.Tests
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Icm.Api.Host.Contracts;
    using Icm.Api.Host.Services;
    using Icm.Api.Models;

    /// <summary>
    /// The shared sample is what MyssApi's provider sends. This host must read
    /// every property of it, and read it into the library's terms unchanged.
    /// </summary>
    public class BusPassContractTests
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            // An unknown property is a contract drift, not something to ignore.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() },
        };

        [Fact]
        public async Task TheSharedSample_IsReadInFull()
        {
            string json = await File.ReadAllTextAsync("bus-pass-application.sample.json");
            using JsonDocument document = JsonDocument.Parse(json);

            // The "//" note is for readers of the file, not part of the contract.
            var withoutNote = new Dictionary<string, JsonElement>();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Name != "//")
                {
                    withoutNote[property.Name] = property.Value;
                }
            }

            BusPassApplicationRequest? request = JsonSerializer.Deserialize<BusPassApplicationRequest>(
                JsonSerializer.Serialize(withoutNote), Options);

            Assert.NotNull(request);
            BusPassApplication application = BusPassApplicationRequestMapper.ToApplication(request);

            Assert.Equal("6f1c2a3b-0000-4000-8000-000000000001", application.SubmissionKey);
            Assert.Equal(BusPassRequestType.AddressUpdate, application.RequestType);
            Assert.Equal(BusPassApplicantType.Over65, application.ApplicantType);
            Assert.False(application.AcknowledgedPassCancellation);
            Assert.True(application.AcknowledgedEligibilityCriteria);
            Assert.Equal("046454286", application.SocialInsuranceNumber);
            Assert.Equal("12345678", application.BusPassAccountNumber);
            Assert.Equal("Myss", application.FirstName);
            Assert.Equal("IntegrationTest", application.LastName);
            Assert.Equal(new DateOnly(1950, 1, 1), application.DateOfBirth);
            Assert.Equal("2505550199", application.PhoneNumber);
            Assert.Equal(BusPassPhoneType.Home, application.PhoneType);
            Assert.True(application.LeaveMessageAllowed);
            Assert.Equal("myss.integrationtest@example.com", application.EmailAddress);
            Assert.Equal(BusPassContactMethod.Email, application.PreferredContactMethod);

            Assert.NotNull(application.ResidentialAddress);
            Assert.Equal("1", application.ResidentialAddress.Unit);
            Assert.Equal("501 Belleville St", application.ResidentialAddress.Line1);
            Assert.Equal("Rear entrance", application.ResidentialAddress.Line2);
            Assert.Equal("Victoria", application.ResidentialAddress.City);
            Assert.Equal("BC", application.ResidentialAddress.Province);
            Assert.Equal("V8V 1X4", application.ResidentialAddress.PostalCode);

            Assert.NotNull(application.MailingAddress);
            Assert.Equal("PO Box 9041 Stn Prov Govt", application.MailingAddress.Line1);
            Assert.Equal("V8W 9E1", application.MailingAddress.PostalCode);
        }

        [Fact]
        public void TheResponse_CarriesTheFourFieldsMyssApiReads_AndNotTheName()
        {
            var result = new BusPassResult
            {
                ApplicationNumber = "1-TEST-0001",
                ErrorCode = string.Empty,
                ErrorMessage = null,
                Status = "SUCCESS",
                FirstName = "Someone",
                LastName = "Else",
            };

            string json = JsonSerializer.Serialize(BusPassApplicationResponse.From(result), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using JsonDocument document = JsonDocument.Parse(json);

            string[] names = [.. document.RootElement.EnumerateObject().Select(p => p.Name).Order()];
            Assert.Equal(["applicationNumber", "errorCode", "errorMessage", "status"], names);

            // An empty error code from the workflow is "no error", which MyssApi
            // reads as accepted either way; null keeps the contract unambiguous.
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("errorCode").ValueKind);
        }
    }
}
