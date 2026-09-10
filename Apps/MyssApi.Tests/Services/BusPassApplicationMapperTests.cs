namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="BusPassApplicationMapper"/>: answers in, the
    /// middleware's business-language request out.
    /// </summary>
    public class BusPassApplicationMapperTests
    {
        [Fact]
        public void NewApplicant_MapsRequestTypeCategoryAndAcknowledgement()
        {
            BusPassApplicationModel request = Build("""
                {"applicantCategory":"new","eligibilityCategory":"firstNations","eligibilityAcknowledged":true}
                """);

            Assert.Equal(BusPassRequestType.NewApplication, request.RequestType);
            Assert.Equal(BusPassApplicantType.FirstNations, request.ApplicantType);
            Assert.True(request.AcknowledgedEligibilityCriteria);
        }

        [Theory]
        [InlineData("moved", BusPassRequestType.AddressUpdate)]
        [InlineData("replacement", BusPassRequestType.Replacement)]
        public void ExistingClient_MapsTheReasonToTheRequestType(string reason, BusPassRequestType expected)
        {
            BusPassApplicationModel request = Build($$"""
                {"applicantCategory":"existing","existingClientReason":"{{reason}}","eligibilityCategory":"over65","eligibilityAcknowledged":true}
                """);

            Assert.Equal(expected, request.RequestType);

            // New-applicant facts do not travel on an existing-client request,
            // even if the browser left stale values behind.
            Assert.Null(request.ApplicantType);
            Assert.Null(request.AcknowledgedEligibilityCriteria);
        }

        [Fact]
        public void IdentifiersAndPhone_AreSentAsDigitsOnly()
        {
            BusPassApplicationModel request = Build("""
                {"socialInsuranceNumber":"046 454 286","busPassAccountNumber":"1234-5678-9","phoneNumber":"(250) 555-0199"}
                """);

            Assert.Equal("046454286", request.SocialInsuranceNumber);
            Assert.Equal("123456789", request.BusPassAccountNumber);
            Assert.Equal("2505550199", request.PhoneNumber);
        }

        [Fact]
        public void BlankIdentifier_IsNullNotEmpty()
        {
            BusPassApplicationModel request = Build("""{"socialInsuranceNumber":"___ ___ ___","busPassAccountNumber":""}""");

            Assert.Null(request.SocialInsuranceNumber);
            Assert.Null(request.BusPassAccountNumber);
        }

        [Fact]
        public void DateOfBirth_IsFoldedFromTheThreeParts()
        {
            BusPassApplicationModel request = Build("""{"birthDay":"5","birthMonth":"05","birthYear":"1950"}""");

            Assert.Equal(new DateOnly(1950, 5, 5), request.DateOfBirth);
        }

        [Fact]
        public void ContactPreferences_MapToTheEnums()
        {
            BusPassApplicationModel request = Build("""
                {"phoneType":"cell","leaveMessage":true,"email":" ada@example.com ","preferredCommunication":"email"}
                """);

            Assert.Equal(BusPassPhoneType.Cell, request.PhoneType);
            Assert.True(request.LeaveMessageAllowed);
            Assert.Equal("ada@example.com", request.EmailAddress);
            Assert.Equal(BusPassContactMethod.Email, request.PreferredContactMethod);
        }

        [Fact]
        public void UnknownCodes_MapToNullRatherThanGuessing()
        {
            BusPassApplicationModel request = Build("""{"phoneType":"pager","preferredCommunication":"fax","eligibilityCategory":"other","applicantCategory":"new"}""");

            Assert.Null(request.PhoneType);
            Assert.Null(request.PreferredContactMethod);
            Assert.Null(request.ApplicantType);
        }

        [Fact]
        public void ResidentialAddress_IsAlwaysSentWithTheProvinceNormalised()
        {
            BusPassApplicationModel request = Build("""
                {"streetAddress1":"501 Belleville St","streetAddress2":"Suite 2","city":"Victoria","province":"British Columbia","postalCode":"V8V 1X4"}
                """);

            Assert.NotNull(request.ResidentialAddress);
            Assert.Equal("501 Belleville St", request.ResidentialAddress.Line1);
            Assert.Equal("Suite 2", request.ResidentialAddress.Line2);
            Assert.Equal("Victoria", request.ResidentialAddress.City);
            Assert.Equal("BC", request.ResidentialAddress.Province);
            Assert.Equal("V8V 1X4", request.ResidentialAddress.PostalCode);
        }

        [Fact]
        public void MailingAddress_IsOnlySentWhenItDiffers()
        {
            BusPassApplicationModel same = Build("""
                {"mailingAddressDifferent":"no","mailingStreetAddress1":"stale","mailingCity":"stale"}
                """);
            BusPassApplicationModel different = Build("""
                {"mailingAddressDifferent":"yes","mailingStreetAddress1":"PO Box 9985","mailingCity":"Victoria","mailingProvince":"BC","mailingPostalCode":"V8W 9R6"}
                """);

            Assert.Null(same.MailingAddress);
            Assert.NotNull(different.MailingAddress);
            Assert.Equal("PO Box 9985", different.MailingAddress.Line1);
            Assert.Equal("V8W 9R6", different.MailingAddress.PostalCode);
        }

        [Fact]
        public void ReplacementFeeAcknowledgement_HasNoFieldYetAndIsNotSent()
        {
            BusPassApplicationModel request = Build("""{"applicantCategory":"existing","existingClientReason":"replacement"}""");

            Assert.Null(request.AcknowledgedPassCancellation);
        }

        [Fact]
        public void Request_SerialisesEnumsAsWordsForTheMiddleware()
        {
            BusPassApplicationModel request = Build("""{"applicantCategory":"new","eligibilityCategory":"over65","phoneType":"home","preferredCommunication":"phone"}""");

            string json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.Contains("\"requestType\":\"NewApplication\"", json, StringComparison.Ordinal);
            Assert.Contains("\"applicantType\":\"Over65\"", json, StringComparison.Ordinal);
            Assert.Contains("\"phoneType\":\"Home\"", json, StringComparison.Ordinal);
            Assert.Contains("\"preferredContactMethod\":\"Phone\"", json, StringComparison.Ordinal);
        }

        private static BusPassApplicationModel Build(string answersJson)
        {
            using JsonDocument doc = JsonDocument.Parse(answersJson);
            return BusPassApplicationMapper.Build(doc.RootElement.Clone());
        }
    }
}
